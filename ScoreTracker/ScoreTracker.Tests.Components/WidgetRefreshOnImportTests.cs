using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

// Pins which refresh signal each widget subscribes to (owner, 2026-08-30): a widget that reads
// the scores themselves reloads the moment the import finishes saving, the few whose data IS the
// post-batch analysis reload when the recalculated stats land, and the recorder / importer /
// community feed subscribe to neither.
public sealed class WidgetRefreshOnImportTests
{
    [Theory]
    [InlineData("weekly-challenge")]
    [InlineData("daily-step")]
    [InlineData("folder-levels")]
    [InlineData("suggested-charts")]
    [InlineData("by-level-breakdown")]
    public void ScoreReadingWidgetsRefreshWhenTheImportFinishesSaving(string typeId)
    {
        Assert.True(WidgetRegistry.TryGet(typeId)!.RefreshOnScoreImport);
    }

    [Theory]
    [InlineData("competitive-level")]
    [InlineData("pumbility")]
    // The straddler: most goals read scores, but Pumbility Push gains ride the projections.
    [InlineData("suggested-charts")]
    public void AnalysisReadingWidgetsRefreshWhenRecalculatedStatsLand(string typeId)
    {
        Assert.True(WidgetRegistry.TryGet(typeId)!.RefreshOnStatsUpdate);
    }

    [Theory]
    [InlineData("competitive-level")]
    [InlineData("pumbility")]
    public void AnalysisOnlyWidgetsDoNotReloadAtImportTime(string typeId)
    {
        // Their data (level history, stored ratings) doesn't exist until the analysis runs —
        // an import-time reload would refetch the old numbers and read as "nothing changed".
        Assert.False(WidgetRegistry.TryGet(typeId)!.RefreshOnScoreImport);
    }

    [Theory]
    [InlineData("weekly-challenge")]
    [InlineData("daily-step")]
    [InlineData("folder-levels")]
    [InlineData("by-level-breakdown")]
    public void ScoreOnlyWidgetsDoNotAlsoReloadWhenStatsLand(string typeId)
    {
        Assert.False(WidgetRegistry.TryGet(typeId)!.RefreshOnStatsUpdate);
    }

    [Theory]
    [InlineData("import-scores")]
    [InlineData("quick-record")]
    [InlineData("community-highlights")]
    public void OtherWidgetsSubscribeToNeitherSignal(string typeId)
    {
        var descriptor = WidgetRegistry.TryGet(typeId)!;
        Assert.False(descriptor.RefreshOnScoreImport);
        Assert.False(descriptor.RefreshOnStatsUpdate);
    }
}
