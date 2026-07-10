using ScoreTracker.WeeklyChallenge.Contracts.Messages;
using ScoreTracker.ChartIntelligence.Contracts.Messages;
using MassTransit;
using ScoreTracker.EventCompetition.Contracts.Messages;
using ScoreTracker.Identity.Contracts.Messages;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.ScoreLedger.Contracts.Messages;
using ScoreTracker.SharedKernel.Enums;

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
        _bus.Publish(new RecalculateScoringDifficultyCommand());

    public Task PublishUpdateWeeklyCharts() =>
        _bus.Publish(new RotateWeeklyChartsCommand());

    public Task PublishProcessPassTierList() =>
        _bus.Publish(new ProcessPassTierListCommand());

    public Task PublishCalculateChartLetterDifficulties() =>
        _bus.Publish(new RecalculateChartLetterDifficultiesCommand());

    public Task PublishStartLeaderboardImport() =>
        _bus.Publish(new StartLeaderboardImportCommand());

    public Task PublishStartPhoenix2LeaderboardImport() =>
        _bus.Publish(new StartLeaderboardImportCommand(MixEnum.Phoenix2));

    public Task PublishTryScheduleMoM() =>
        _bus.Publish(new TryScheduleMoMCommand());

    public Task PublishFlushOverdueScoreBatches() =>
        _bus.Publish(new FlushOverdueScoreBatchesCommand());

    public Task PublishProcessAccountPurges() =>
        _bus.Publish(new ProcessAccountPurgesCommand());
}
