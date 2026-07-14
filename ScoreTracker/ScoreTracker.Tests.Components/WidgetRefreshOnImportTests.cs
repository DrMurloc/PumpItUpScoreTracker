using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

// Pins which widgets the dashboard auto-refreshes after a score import — personal-score widgets in,
// the recorder / importer / community feed out.
public sealed class WidgetRefreshOnImportTests
{
    [Theory]
    [InlineData("competitive-level")]
    [InlineData("pumbility")]
    [InlineData("weekly-challenge")]
    [InlineData("daily-step")]
    [InlineData("suggested-charts")]
    [InlineData("by-level-breakdown")]
    public void PersonalScoreWidgetsRefreshOnImport(string typeId)
    {
        Assert.True(WidgetRegistry.TryGet(typeId)!.RefreshOnScoreImport);
    }

    [Theory]
    [InlineData("import-scores")]
    [InlineData("quick-record")]
    [InlineData("community-highlights")]
    public void OtherWidgetsDoNotRefreshOnImport(string typeId)
    {
        Assert.False(WidgetRegistry.TryGet(typeId)!.RefreshOnScoreImport);
    }
}
