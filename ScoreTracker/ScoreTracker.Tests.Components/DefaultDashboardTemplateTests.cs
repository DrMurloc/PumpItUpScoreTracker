using ScoreTracker.HomePage.Contracts;
using ScoreTracker.Web.Components.HomeWidgets;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The curated default (DefaultDashboardTemplate) is seeded blind by "Create", so a
///     registry drift — a widget renamed, a size dropped, the cap lowered — would ship a
///     broken first-run board that no one adds by hand. This pins it: every TypeId
///     resolves, every size is one the widget actually supports, the configs round-trip
///     to what we intend, and nothing pins a mix (D13: all follow the current mix).
/// </summary>
public sealed class DefaultDashboardTemplateTests
{
    [Fact]
    public void EveryEntryResolvesAndUsesASupportedSize()
    {
        foreach (var entry in DefaultDashboardTemplate.Entries)
        {
            var descriptor = WidgetRegistry.TryGet(entry.TypeId);
            Assert.True(descriptor != null, $"Template references unknown widget '{entry.TypeId}'.");

            var size = SizePreset.TryParse(entry.SizeToken);
            Assert.True(size != null, $"Template size '{entry.SizeToken}' for '{entry.TypeId}' is unparseable.");
            Assert.True(descriptor!.SupportedSizes.Contains(size!.Value),
                $"'{entry.TypeId}' does not support size {entry.SizeToken}.");
        }
    }

    [Fact]
    public void FitsWithinTheWidgetCap()
    {
        Assert.True(DefaultDashboardTemplate.Entries.Count <= HomePageRecord.MaxWidgetsPerPage,
            $"The default seeds {DefaultDashboardTemplate.Entries.Count} widgets, over the " +
            $"{HomePageRecord.MaxWidgetsPerPage} cap.");
    }

    [Fact]
    public void IsTheAgreedLayoutInOrder()
    {
        // Order is the Tetris packing — a change here is a deliberate layout change.
        var expected = new[]
        {
            ("pumbility", "1x2"),
            ("suggested-charts", "1x2"),
            ("import-scores", "1x1"),
            ("daily-step", "1x2"),
            ("community-highlights", "1x3"),
            ("by-level-breakdown", "2x2"),
            ("weekly-challenge", "1x2"),
            ("suggested-charts", "4x1")
        };
        var actual = DefaultDashboardTemplate.Entries.Select(e => (e.TypeId, e.SizeToken)).ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NoWidgetPinsAMix()
    {
        // "Default Mix" = leave Mix null so every widget follows the page/current mix.
        foreach (var entry in DefaultDashboardTemplate.Entries)
        {
            switch (entry.TypeId)
            {
                case "suggested-charts":
                    Assert.Null(WidgetConfigJson.Read<SuggestedChartsConfig>(entry.ConfigJson).Mix);
                    break;
                case "by-level-breakdown":
                    Assert.Null(WidgetConfigJson.Read<ByLevelBreakdownConfig>(entry.ConfigJson).Mix);
                    break;
            }
        }
    }

    [Fact]
    public void SuggestedGoalsAreTitleHuntAndPumbilityPush()
    {
        var goals = DefaultDashboardTemplate.Entries
            .Where(e => e.TypeId == "suggested-charts")
            .Select(e => WidgetConfigJson.Read<SuggestedChartsConfig>(e.ConfigJson).Goal)
            .ToArray();
        Assert.Equal(new[] { SuggestedGoal.PumbilityPush, SuggestedGoal.TitleHunt }, goals);
    }

    [Fact]
    public void FolderCompletionIsCombinedClearsAcrossEveryLevel()
    {
        var folder = DefaultDashboardTemplate.Entries.Single(e => e.TypeId == "by-level-breakdown");
        var config = WidgetConfigJson.Read<ByLevelBreakdownConfig>(folder.ConfigJson);
        Assert.Equal(BreakdownMetric.Pass, config.Metric);
        Assert.Equal(BreakdownAggregation.Breakdown, config.Aggregation);
        Assert.False(config.SeparateSinglesDoubles);
        Assert.Equal(1, config.MinLevel);
        Assert.Equal(29, config.MaxLevel);
    }
}
