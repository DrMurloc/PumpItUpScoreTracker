using MassTransit;
using ScoreTracker.Application.Messages;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Events;

namespace ScoreTracker.Web.HostedServices;

public sealed class RecurringJobRunner
{
    private readonly IBus _bus;

    public RecurringJobRunner(IBus bus)
    {
        _bus = bus;
    }

    public Task PublishProcessScoresTiersList() =>
        _bus.Publish(new ProcessScoresTiersListCommand());

    public Task PublishCalculateScoringDifficulty() =>
        _bus.Publish(new RecalculateScoringDifficulty());

    public Task PublishUpdateWeeklyCharts() =>
        _bus.Publish(new RotateWeeklyCharts());

    public Task PublishProcessPassTierList() =>
        _bus.Publish(new ProcessPassTierListCommand());

    public Task PublishCalculateChartLetterDifficulties() =>
        _bus.Publish(new RecalculateChartLetterDifficulties());

    public Task PublishStartLeaderboardImport() =>
        _bus.Publish(new StartLeaderboardImport());

    public Task PublishTryScheduleMoM() =>
        _bus.Publish(new MarchOfMurlocsHandler.TryScheduleMoM());

    public Task PublishFlushOverdueScoreBatches() =>
        _bus.Publish(new FlushOverdueScoreBatches());
}
