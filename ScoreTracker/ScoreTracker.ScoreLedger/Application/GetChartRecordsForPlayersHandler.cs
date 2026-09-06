using MediatR;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     Reads the whole chart off its index and keeps the named players in memory. The chart
///     side is one indexed range; the player side is a set that can be several hundred long,
///     which as a SQL parameter list plans badly and as a hash lookup costs nothing.
/// </summary>
internal sealed class GetChartRecordsForPlayersHandler
    : IRequestHandler<GetChartRecordsForPlayersQuery, IReadOnlyList<PlayerChartRecord>>
{
    private readonly IPhoenixRecordRepository _records;

    public GetChartRecordsForPlayersHandler(IPhoenixRecordRepository records)
    {
        _records = records;
    }

    public async Task<IReadOnlyList<PlayerChartRecord>> Handle(GetChartRecordsForPlayersQuery request,
        CancellationToken cancellationToken)
    {
        if (request.UserIds.Count == 0) return Array.Empty<PlayerChartRecord>();

        var wanted = request.UserIds as IReadOnlySet<Guid> ?? request.UserIds.ToHashSet();
        return (await _records.GetRecordedScoresForChart(request.Mix, request.ChartId, cancellationToken))
            .Where(r => wanted.Contains(r.UserId))
            .Select(r => new PlayerChartRecord(r.UserId, r.Record))
            .ToArray();
    }
}
