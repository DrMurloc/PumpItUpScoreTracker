using MassTransit;
using MediatR;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     Counting and withdrawing broken personal bests — the Your Data cleanup for records made
///     while "Record broken scores as your best" was on (docs/design/delete-my-data.md §10).
/// </summary>
internal sealed class BrokenRecordCleanupSaga : IRequestHandler<GetBrokenRecordCountsQuery,
        IReadOnlyList<BrokenRecordCount>>,
    IRequestHandler<DeleteBrokenRecordsCommand, int>
{
    private readonly IBus _bus;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IPhoenixRecordRepository _records;

    public BrokenRecordCleanupSaga(IPhoenixRecordRepository records, IBus bus,
        IDateTimeOffsetAccessor dateTime)
    {
        _records = records;
        _bus = bus;
        _dateTime = dateTime;
    }

    public async Task<int> Handle(DeleteBrokenRecordsCommand request, CancellationToken cancellationToken)
    {
        var removed = 0;
        foreach (var mix in Cleanable(request.Mixes))
        {
            var count = await _records.DeleteBrokenRecords(mix, request.UserId, cancellationToken);
            if (count == 0) continue;

            removed += count;
            // Pumbility, titles and folder lamps are computed from the records that just went, so
            // they are announced rather than chosen (delete-my-data.md D9). An empty change list
            // is the established "recompute from what is left" signal — the same one a scoped wipe
            // publishes — and the progression pipelines run their rating and title steps on it.
            await _bus.Publish(
                PlayerScoresUpdatedEvent.Create(_dateTime.Now, request.UserId, mix,
                    Array.Empty<PlayerScoresUpdatedEvent.ScoreChange>()),
                cancellationToken);
        }

        return removed;
    }

    public async Task<IReadOnlyList<BrokenRecordCount>> Handle(GetBrokenRecordCountsQuery request,
        CancellationToken cancellationToken)
    {
        var counts = new List<BrokenRecordCount>();
        foreach (var mix in Cleanable(Enum.GetValues<MixEnum>()))
            counts.Add(new BrokenRecordCount(mix,
                await _records.CountBrokenRecords(mix, request.UserId, cancellationToken)));

        return counts;
    }

    /// <summary>
    ///     A legacy mix records a letter grade in <c>BestAttempt</c>, which has no failed-stage
    ///     flag to read or clear — so one is dropped rather than queried. Filtering beats refusing
    ///     here: nothing is being corrected, the answer for those mixes is genuinely nothing.
    /// </summary>
    private static IEnumerable<MixEnum> Cleanable(IEnumerable<MixEnum> mixes)
    {
        return mixes.Distinct().Where(m => !m.UsesLegacyScoring());
    }
}
