using System.IO;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.Web.Components.HomeWidgets;
using ScoreTracker.Web.Services.HomeDashboard;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     Pins the D19 public documents: the page export/import envelope and the
///     capability schema. These are API contracts exactly like api/* — a failing
///     assertion here means the public dashboard vocabulary changed (a widget TypeId,
///     a size token, a config record shape), and that is breaking-change review, not a
///     test to casually update.
/// </summary>
public sealed class HomePageDocumentContractTests
{
    private static HomePageRecord SamplePage()
    {
        return new HomePageRecord(Guid.Empty, "Session Day", 0, true, SharedKernel.Enums.MixEnum.Phoenix2,
            new[]
            {
                new HomePageWidgetRecord(Guid.Empty, "pumbility", "Doubles push", 0, "2x1",
                    "{\"mix\":\"Phoenix2\",\"showProjections\":true}", 1),
                new HomePageWidgetRecord(Guid.Empty, "weekly-challenge", null, 1, "1x1", "{}", 1),
                new HomePageWidgetRecord(Guid.Empty, "by-level-breakdown", "PG chase", 2, "2x2",
                    WidgetConfigJson.Write(new ByLevelBreakdownConfig
                    {
                        Mix = SharedKernel.Enums.MixEnum.Phoenix2,
                        Metric = BreakdownMetric.Score,
                        Aggregation = BreakdownAggregation.Completion,
                        SeparateSinglesDoubles = true,
                        MinLevel = 20,
                        MaxLevel = 24,
                        Thresholds = new List<CompletionThreshold>
                        {
                            new() { Kind = ThresholdKind.Score, Value = "990000" },
                            new() { Kind = ThresholdKind.Score, Value = "1000000" }
                        }
                    }), 1)
            });
    }

    [Fact]
    public void ExportDocumentWireShapeIsPinned()
    {
        AssertMatchesGolden("page-export.json", PageExportService.Export(SamplePage()));
    }

    [Fact]
    public void CapabilitySchemaIsPinned()
    {
        AssertMatchesGolden("capability-schema.json", CapabilitySchemaService.Build());
    }

    private static void AssertMatchesGolden(string goldenFile, string actual)
    {
        // Deliberate contract changes regenerate with REGENERATE_GOLDENS=1 — the
        // rewritten golden then shows up as a reviewed diff, which is the point.
        if (Environment.GetEnvironmentVariable("REGENERATE_GOLDENS") == "1")
        {
            File.WriteAllText(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Goldens", goldenFile), actual);
            return;
        }

        var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Goldens", goldenFile));
        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    private static string Normalize(string json)
    {
        return json.Replace("\r\n", "\n").Trim();
    }

    [Fact]
    public void ImportRoundTripsExport()
    {
        var result = PageExportService.Parse(PageExportService.Export(SamplePage()));

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Equal("Session Day", result.Name);
        Assert.Equal(SharedKernel.Enums.MixEnum.Phoenix2, result.DefaultMix);
        Assert.Equal(3, result.Widgets.Count);
        Assert.Equal("pumbility", result.Widgets[0].WidgetType);
        Assert.Equal("2x1", result.Widgets[0].SizePreset);

        // The By-Level Breakdown config survives the public export/import round-trip,
        // including its discriminated {kind, value} threshold list (decision D).
        Assert.Equal("by-level-breakdown", result.Widgets[2].WidgetType);
        var breakdown = WidgetConfigJson.Read<ByLevelBreakdownConfig>(result.Widgets[2].ConfigJson);
        Assert.Equal(BreakdownAggregation.Completion, breakdown.Aggregation);
        Assert.Equal(2, breakdown.Thresholds.Count);
        Assert.Equal(ThresholdKind.Score, breakdown.Thresholds[1].Kind);
        Assert.Equal("1000000", breakdown.Thresholds[1].Value);
    }
}
