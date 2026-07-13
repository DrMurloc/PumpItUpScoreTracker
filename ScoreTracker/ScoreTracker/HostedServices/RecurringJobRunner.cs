using ScoreTracker.WeeklyChallenge.Contracts.Messages;
using ScoreTracker.Catalog.Contracts.Messages;
using ScoreTracker.Communities.Contracts.Messages;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Messages;
using MassTransit;
using ScoreTracker.EventCompetition.Contracts.Messages;
using ScoreTracker.Identity.Contracts.Messages;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.ScoreLedger.Contracts.Messages;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services.Theming;

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

    // Daily Step runs parallel per-mix boards (owner); the daily cadence can't rely on the manual
    // per-mix trigger the Weekly page uses, so the job fans out to each supported mix. A mix without
    // a chart catalog yet no-ops in the consumer.
    public Task PublishRotateDailyStep() =>
        Task.WhenAll(
            _bus.Publish(new RotateDailyStepCommand(MixEnum.Phoenix)),
            _bus.Publish(new RotateDailyStepCommand(MixEnum.Phoenix2)));

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

    public Task PublishRefreshFolderShareCards() =>
        _bus.Publish(new RefreshFolderShareCardsCommand(ShareTheme(MixEnum.Phoenix)));

    public Task PublishCrawlPiuCenter() =>
        _bus.Publish(new CrawlPiuCenterCommand());

    public Task PublishPurgeCommunityHighlights() =>
        _bus.Publish(new PurgeCommunityHighlightsCommand());

    // The Web layer resolves presentation (MixThemes is the single palette source);
    // the vertical's share-card saga stays palette-blind.
    private static FolderShareCardTheme ShareTheme(MixEnum mix)
    {
        var palette = MixThemes.PaletteFor(mix);
        return new FolderShareCardTheme(palette.Background, palette.Surface, palette.Ink, palette.InkMuted,
            palette.Primary, Enum.GetValues<TierListCategory>().ToDictionary(c => c, MixThemes.DifficultyHex));
    }
}
