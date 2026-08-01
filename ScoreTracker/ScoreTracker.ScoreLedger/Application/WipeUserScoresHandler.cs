using MassTransit;
using MediatR;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Application;

internal sealed class WipeUserScoresHandler : IRequestHandler<WipeUserScoresCommand>
{
    // The mixes with parallel derived state (stats/titles/history pipelines). XX keeps its
    // legacy tables and never rides the PlayerScoresUpdatedEvent pipelines.
    private static readonly MixEnum[] ParallelMixes = { MixEnum.Phoenix, MixEnum.Phoenix2 };

    private readonly IBus _bus;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IScoreJournalRepository _journal;
    private readonly IPhoenixRecordRepository _phoenixScores;
    private readonly IPlayerStatsRepository _playerStats;
    private readonly ITitleRepository _titles;
    private readonly IXXChartAttemptRepository _xxScores;

    public WipeUserScoresHandler(IPhoenixRecordRepository phoenixScores,
        IXXChartAttemptRepository xxScores,
        IPlayerStatsRepository playerStats,
        ITitleRepository titles,
        IScoreJournalRepository journal,
        IBus bus,
        IDateTimeOffsetAccessor dateTime)
    {
        _dateTime = dateTime;
        _phoenixScores = phoenixScores;
        _xxScores = xxScores;
        _playerStats = playerStats;
        _titles = titles;
        _journal = journal;
        _bus = bus;
    }

    public async Task Handle(WipeUserScoresCommand request, CancellationToken cancellationToken)
    {
        var items = request.Items;
        if (items == ScoreDeletionItems.None || request.Mixes.Count == 0) return;

        foreach (var mix in request.Mixes.Distinct())
        {
            if (items.HasFlag(ScoreDeletionItems.BestScores))
            {
                await _phoenixScores.DeleteAllForUser(request.UserId, mix, cancellationToken);
                await _xxScores.DeleteAllForUser(request.UserId, mix, cancellationToken);
            }

            // The journal goes with a wipe. It used to survive one, which made "delete my
            // scores" quietly untrue — the plays were still there, chart by chart (D8).
            if (items.HasFlag(ScoreDeletionItems.PlayHistory))
                await _journal.DeleteForUser(request.UserId, mix, cancellationToken);

            // Rating history, highlights and milestones are PlayerProgress's. None of them are
            // recomputed from scores, so deleting the scores behind them strands them rather
            // than clearing them — it has to be told.
            var progression = new PlayerScoreDataDeletedEvent(request.UserId, mix,
                items.HasFlag(ScoreDeletionItems.RatingHistory),
                items.HasFlag(ScoreDeletionItems.Highlights),
                items.HasFlag(ScoreDeletionItems.Milestones));
            if (progression.AnythingToDo) await _bus.Publish(progression, cancellationToken);

            // Derived per-mix state is recomputed from the records that just went, so it is
            // reset rather than chosen — and only for the mixes that have such a pipeline.
            if (!items.HasFlag(ScoreDeletionItems.BestScores) || !ParallelMixes.Contains(mix)) continue;

            await _playerStats.DeleteStats(mix, request.UserId, cancellationToken);
            await _titles.DeleteHighestTitle(mix, request.UserId, cancellationToken);
            await _bus.Publish(
                PlayerScoresUpdatedEvent.Create(_dateTime.Now, request.UserId, mix,
                    Array.Empty<PlayerScoresUpdatedEvent.ScoreChange>()),
                cancellationToken);
        }
    }
}
