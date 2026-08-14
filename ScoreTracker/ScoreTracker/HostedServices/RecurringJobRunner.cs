using ScoreTracker.WeeklyChallenge.Contracts.Messages;
using ScoreTracker.Catalog.Contracts.Messages;
using ScoreTracker.CommunityTools.Contracts.Messages;
using ScoreTracker.ChartIntelligence.Contracts.Messages;
using MassTransit;
using ScoreTracker.EventCompetition.Contracts.Messages;
using ScoreTracker.Identity.Contracts.Messages;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.PlayerProgress.Contracts.Messages;
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

    public Task PublishRetryDueWebhookDeliveries() =>
        _bus.Publish(new RetryDueWebhookDeliveriesCommand());

    public Task PublishPruneWebhookDeliveries() =>
        _bus.Publish(new PruneWebhookDeliveriesCommand());

    public Task PublishResetDeepScans() =>
        _bus.Publish(new ResetDeepScansCommand());

    // One command, two consumers: ScoreLedger drains batches sitting past their deadline,
    // OfficialMirror replays sessions whose batch is gone.
    public Task PublishFlushOverdueScoreBatches() =>
        _bus.Publish(new FlushOverdueScoreBatchesCommand());

    // Phoenix 2 tier lists are live (owner, 2026-08-13): every tier-list compute job fans out
    // per mix, like the rotations below. Each consumer's own thin-data guards keep a mix with
    // little volume quiet rather than wrong — the PUMBILITY job's full-pool gate in particular
    // writes nothing until a mix has real pools.
    public Task PublishProcessScoresTiersList() =>
        Task.WhenAll(
            _bus.Publish(new ProcessScoresTiersListCommand(MixEnum.Phoenix)),
            _bus.Publish(new ProcessScoresTiersListCommand(MixEnum.Phoenix2)));

    public Task PublishCalculateScoringDifficulty() =>
        Task.WhenAll(
            _bus.Publish(new RecalculateScoringDifficultyCommand(MixEnum.Phoenix)),
            _bus.Publish(new RecalculateScoringDifficultyCommand(MixEnum.Phoenix2)));

    // Weekly boards are parallel per mix (like Daily Step below). A daily cadence can't rely on the
    // manual per-mix trigger the Weekly page uses, so the job fans out to each supported mix; a mix
    // without a chart catalog yet no-ops in the consumer.
    public Task PublishUpdateWeeklyCharts() =>
        Task.WhenAll(
            _bus.Publish(new RotateWeeklyChartsCommand(MixEnum.Phoenix)),
            _bus.Publish(new RotateWeeklyChartsCommand(MixEnum.Phoenix2)));

    // Daily Step runs parallel per-mix boards (owner); the daily cadence can't rely on the manual
    // per-mix trigger the Weekly page uses, so the job fans out to each supported mix. A mix without
    // a chart catalog yet no-ops in the consumer.
    public Task PublishRotateDailyStep() =>
        Task.WhenAll(
            _bus.Publish(new RotateDailyStepCommand(MixEnum.Phoenix)),
            _bus.Publish(new RotateDailyStepCommand(MixEnum.Phoenix2)));

    public Task PublishProcessPassTierList() =>
        Task.WhenAll(
            _bus.Publish(new ProcessPassTierListCommand(MixEnum.Phoenix)),
            _bus.Publish(new ProcessPassTierListCommand(MixEnum.Phoenix2)));

    public Task PublishProcessPumbilityTierList() =>
        Task.WhenAll(
            _bus.Publish(new ProcessPumbilityTierListCommand(MixEnum.Phoenix)),
            _bus.Publish(new ProcessPumbilityTierListCommand(MixEnum.Phoenix2)));

    public Task PublishCalculateChartLetterDifficulties() =>
        Task.WhenAll(
            _bus.Publish(new RecalculateChartLetterDifficultiesCommand(MixEnum.Phoenix)),
            _bus.Publish(new RecalculateChartLetterDifficultiesCommand(MixEnum.Phoenix2)));

    public Task PublishRecalculateChartSimilarity() =>
        _bus.Publish(new RecalculateChartSimilarityCommand());

    public Task PublishStartLeaderboardImport() =>
        _bus.Publish(new StartLeaderboardImportCommand());

    public Task PublishStartPhoenix2LeaderboardImport() =>
        _bus.Publish(new StartLeaderboardImportCommand(MixEnum.Phoenix2));

    public Task PublishTryScheduleMoM() =>
        _bus.Publish(new TryScheduleMoMCommand());

    public Task PublishProcessAccountPurges() =>
        _bus.Publish(new ProcessAccountPurgesCommand());

    public Task PublishCrawlPiuCenter() =>
        _bus.Publish(new CrawlPiuCenterCommand());

    // One command, two consumers: PlayerProgress drops the win payloads, Communities drops its
    // audience index rows over them (docs/design/rivals.md D33).
    public Task PublishPurgePlayerHighlights() =>
        _bus.Publish(new PurgePlayerHighlightsCommand());
}
