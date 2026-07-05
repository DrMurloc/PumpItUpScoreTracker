using MassTransit;
using MediatR;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Application;

internal sealed class WipeUserScoresHandler : IRequestHandler<WipeUserScoresCommand>
{
    // The mixes with parallel derived state (stats/titles/history pipelines). XX keeps its
    // legacy tables and never rides the PlayerScoresUpdatedEvent pipelines.
    private static readonly MixEnum[] ParallelMixes = { MixEnum.Phoenix, MixEnum.Phoenix2 };

    private readonly IPhoenixRecordRepository _phoenixScores;
    private readonly IXXChartAttemptRepository _xxScores;
    private readonly IPlayerStatsRepository _playerStats;
    private readonly ITitleRepository _titles;
    private readonly IPlayerHistoryRepository _playerHistory;
    private readonly IBus _bus;
    private readonly IDateTimeOffsetAccessor _dateTime;

    public WipeUserScoresHandler(IPhoenixRecordRepository phoenixScores,
        IXXChartAttemptRepository xxScores,
        IPlayerStatsRepository playerStats,
        ITitleRepository titles,
        IPlayerHistoryRepository playerHistory,
        IBus bus,
        IDateTimeOffsetAccessor dateTime)
    {
        _dateTime = dateTime;
        _phoenixScores = phoenixScores;
        _xxScores = xxScores;
        _playerStats = playerStats;
        _titles = titles;
        _playerHistory = playerHistory;
        _bus = bus;
    }

    public async Task Handle(WipeUserScoresCommand request, CancellationToken cancellationToken)
    {
        // The score purge itself spans every mix (account-level).
        await _phoenixScores.DeleteAllForUser(request.UserId, cancellationToken);
        await _xxScores.DeleteAllForUser(request.UserId, cancellationToken);
        if (request.IncludeHistory)
            await _playerHistory.DeleteHistoryForUser(request.UserId, cancellationToken);

        // Derived per-mix state is cleared mix-by-mix, and each mix's downstream
        // consumers (stats, titles, communities) are notified for their own slice.
        foreach (var mix in ParallelMixes)
        {
            await _playerStats.DeleteStats(mix, request.UserId, cancellationToken);
            await _titles.DeleteHighestTitle(mix, request.UserId, cancellationToken);

            await _bus.Publish(
                PlayerScoresUpdatedEvent.Create(_dateTime.Now, request.UserId, mix,
                    Array.Empty<PlayerScoresUpdatedEvent.ScoreChange>()),
                cancellationToken);
        }
    }
}
