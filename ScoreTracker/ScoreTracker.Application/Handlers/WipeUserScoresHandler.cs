using MassTransit;
using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class WipeUserScoresHandler : IRequestHandler<WipeUserScoresCommand>
{
    private readonly IPhoenixRecordRepository _phoenixScores;
    private readonly IXXChartAttemptRepository _xxScores;
    private readonly IPlayerStatsRepository _playerStats;
    private readonly ITitleRepository _titles;
    private readonly IPlayerHistoryRepository _playerHistory;
    private readonly IBus _bus;

    public WipeUserScoresHandler(IPhoenixRecordRepository phoenixScores,
        IXXChartAttemptRepository xxScores,
        IPlayerStatsRepository playerStats,
        ITitleRepository titles,
        IPlayerHistoryRepository playerHistory,
        IBus bus)
    {
        _phoenixScores = phoenixScores;
        _xxScores = xxScores;
        _playerStats = playerStats;
        _titles = titles;
        _playerHistory = playerHistory;
        _bus = bus;
    }

    public async Task Handle(WipeUserScoresCommand request, CancellationToken cancellationToken)
    {
        await _phoenixScores.DeleteAllForUser(request.UserId, cancellationToken);
        await _xxScores.DeleteAllForUser(request.UserId, cancellationToken);
        await _playerStats.DeleteStats(request.UserId, cancellationToken);
        await _titles.DeleteHighestTitle(request.UserId, cancellationToken);
        if (request.IncludeHistory)
            await _playerHistory.DeleteHistoryForUser(request.UserId, cancellationToken);

        await _bus.Publish(
            new PlayerScoreUpdatedEvent(request.UserId, Array.Empty<Guid>(), new Dictionary<Guid, int>()),
            cancellationToken);
    }
}
