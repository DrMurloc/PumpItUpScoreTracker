using MudBlazor;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components.HomeWidgets;

namespace ScoreTracker.Web.Services.HomeDashboard;

/// <summary>
///     The widget catalog: one descriptor per widget type (§2.2). A new vertical's
///     widgets register here without any shell changes (D9). Order is the add-drawer
///     display order within categories. TypeIds are public export vocabulary (D19) —
///     never renamed.
/// </summary>
public static class WidgetRegistry
{
    // The rest of the trio lands in C6–C7; catalog widgets one PR at a time (D18).
    private static readonly WidgetDescriptor[] Descriptors =
    {
        new("competitive-level",
            "Competitive Level",
            "Competitive Level over time for your selected mixes.",
            WidgetCategory.Progress,
            Icons.Material.Filled.ShowChart,
            // No 2x1 — a one-row line chart is a smudge, not a graph (owner, round 2).
            new[] { SizePreset.TwoByTwo, SizePreset.ThreeByTwo, SizePreset.ThreeByThree },
            SizePreset.TwoByTwo,
            new[] { MixEnum.Phoenix, MixEnum.Phoenix2 },
            typeof(CompetitiveLevelWidget),
            typeof(CompetitiveLevelConfigPanel),
            typeof(CompetitiveLevelConfig)),
        new("pumbility",
            "PUMBILITY",
            "Pumbility targets tuned to your skill profile.",
            WidgetCategory.Progress,
            Icons.Material.Filled.TrendingUp,
            new[] { SizePreset.OneByOne, SizePreset.TwoByOne, SizePreset.OneByTwo },
            SizePreset.TwoByOne,
            new[] { MixEnum.Phoenix, MixEnum.Phoenix2 },
            typeof(PumbilityWidget),
            typeof(PumbilityConfigPanel),
            typeof(PumbilityConfig)),
        new("weekly-challenge",
            "Weekly Charts",
            "This week's board and your placements.",
            WidgetCategory.Compete,
            Icons.Material.Filled.EmojiEvents,
            new[] { SizePreset.OneByOne, SizePreset.TwoByOne, SizePreset.OneByTwo },
            SizePreset.OneByOne,
            new[] { MixEnum.Phoenix, MixEnum.Phoenix2 },
            typeof(WeeklyWidget),
            typeof(WeeklyConfigPanel),
            typeof(WeeklyConfig)),
        new("daily-step",
            "Daily Step",
            "Today's shared chart, plus a weekly Limbo Day.",
            WidgetCategory.Compete,
            Icons.Material.Filled.Today,
            // 1x1 = top three + you; the taller sizes open into the full scrollable board.
            new[] { SizePreset.OneByOne, SizePreset.OneByTwo, SizePreset.OneByThree },
            SizePreset.OneByOne,
            new[] { MixEnum.Phoenix, MixEnum.Phoenix2 },
            typeof(DailyStepWidget),
            typeof(DailyStepConfigPanel),
            typeof(DailyStepConfig)),
        new("suggested-charts",
            "Suggested Charts",
            "Charts picked for you, tuned by goal.",
            WidgetCategory.Play,
            Icons.Material.Filled.Recommend,
            // 1x3 = the extra-long list (round 4); 3x1/4x1 = wider strips (round 7).
            new[]
            {
                SizePreset.OneByTwo, SizePreset.OneByThree, SizePreset.TwoByOne,
                SizePreset.ThreeByOne, SizePreset.FourByOne, SizePreset.TwoByTwo
            },
            SizePreset.OneByTwo,
            new[] { MixEnum.Phoenix, MixEnum.Phoenix2 },
            typeof(SuggestedChartsWidget),
            typeof(SuggestedChartsConfigPanel),
            typeof(SuggestedChartsConfig),
            // One widget type, goal-preset drawer entries (D10).
            DrawerPresets: new[]
            {
                new WidgetDrawerPreset("Suggested · Title Hunt",
                    "Charts toward your next title, plus skill titles one SSS away.",
                    WidgetConfigJson.Write(new SuggestedChartsConfig { Goal = SuggestedGoal.TitleHunt })),
                new WidgetDrawerPreset("Suggested · Score Push",
                    "Closest PGs, top-50 picks, and old scores due a revisit.",
                    WidgetConfigJson.Write(new SuggestedChartsConfig { Goal = SuggestedGoal.ScorePush })),
                new WidgetDrawerPreset("Suggested · Fill Gaps",
                    "Approachable unpassed charts around your level.",
                    WidgetConfigJson.Write(new SuggestedChartsConfig
                    {
                        Goal = SuggestedGoal.FillGaps,
                        LevelMode = SuggestedLevelMode.Dynamic,
                        // Fills reach down by identity; nothing above by default.
                        SpreadBelow = 3,
                        SpreadAbove = 0
                    }))
            },
            // Instance titles follow the configured goal so rapid-firing all three
            // presets never yields three "Suggested Charts" (owner, field test).
            DynamicNameKey: configJson =>
                WidgetConfigJson.Read<SuggestedChartsConfig>(configJson).Goal switch
                {
                    SuggestedGoal.ScorePush => "Suggested · Score Push",
                    SuggestedGoal.FillGaps => "Suggested · Fill Gaps",
                    _ => "Suggested · Title Hunt"
                },
            RefreshIcon: Icons.Material.Filled.Shuffle,
            RefreshTitleKey: "Shuffle suggestions"),
        new("by-level-breakdown",
            "By-Level Breakdown",
            "One configurable graph of your scores, grades, plates, or clears by level.",
            WidgetCategory.Progress,
            Icons.Material.Filled.BarChart,
            // 2-row minimum: a one-row graph is a smudge (owner, established on W1).
            new[] { SizePreset.TwoByTwo, SizePreset.ThreeByTwo, SizePreset.FourByTwo },
            SizePreset.TwoByTwo,
            // Every recordable mix; the config panel + aggregator restrict metrics for
            // legacy scoring (Grade + Pass only). Read seam already mix-generic.
            Enum.GetValues<MixEnum>(),
            typeof(ByLevelBreakdownWidget),
            typeof(ByLevelBreakdownConfigPanel),
            typeof(ByLevelBreakdownConfig),
            DrawerPresets: new[]
            {
                new WidgetDrawerPreset("Score Distribution",
                    "Score spread per level — min, quartiles, max, with the IQR shaded.",
                    WidgetConfigJson.Write(new ByLevelBreakdownConfig
                    {
                        Metric = BreakdownMetric.Score, Aggregation = BreakdownAggregation.Distribution,
                        Series = new List<DistributionSeries>
                        {
                            DistributionSeries.Min, DistributionSeries.P25, DistributionSeries.Median,
                            DistributionSeries.P75, DistributionSeries.Max
                        },
                        // Combined so the full box plot reads; separate S/D is the simpler
                        // median-vs-median opt-in.
                        Band = BreakdownBand.InterQuartile, SeparateSinglesDoubles = false,
                        MinLevel = 17, MaxLevel = 23
                    })),
                new WidgetDrawerPreset("Grade Wall",
                    "What grades fill each folder, normalized so shape beats volume.",
                    WidgetConfigJson.Write(new ByLevelBreakdownConfig
                    {
                        Metric = BreakdownMetric.LetterGrade, Aggregation = BreakdownAggregation.Breakdown,
                        Normalize = true, IncludeUnplayed = true, SeparateSinglesDoubles = false,
                        MinLevel = 17, MaxLevel = 24
                    })),
                new WidgetDrawerPreset("Plate Distribution",
                    "How each folder's plates stack up — Marvelous, Ultimate, and Perfect Game.",
                    WidgetConfigJson.Write(new ByLevelBreakdownConfig
                    {
                        Metric = BreakdownMetric.Plate, Aggregation = BreakdownAggregation.Completion,
                        Thresholds = new List<CompletionThreshold>
                        {
                            new() { Kind = ThresholdKind.Plate, Value = "MG" },
                            new() { Kind = ThresholdKind.Plate, Value = "UG" },
                            new() { Kind = ThresholdKind.Plate, Value = "PG" }
                        },
                        SeparateSinglesDoubles = true, MinLevel = 17, MaxLevel = 23
                    })),
                new WidgetDrawerPreset("Clear Progress",
                    "How much of every folder you've cleared, Singles vs Doubles.",
                    WidgetConfigJson.Write(new ByLevelBreakdownConfig
                    {
                        Metric = BreakdownMetric.Pass, Aggregation = BreakdownAggregation.Breakdown,
                        SeparateSinglesDoubles = true, MinLevel = 1, MaxLevel = 28
                    })),
                new WidgetDrawerPreset("Score Completion",
                    "How each folder stacks up by score tier: ≥950k, ≥990k, and 1,000,000.",
                    WidgetConfigJson.Write(new ByLevelBreakdownConfig
                    {
                        Metric = BreakdownMetric.Score, Aggregation = BreakdownAggregation.Completion,
                        Thresholds = new List<CompletionThreshold>
                        {
                            new() { Kind = ThresholdKind.Score, Value = "950000" },
                            new() { Kind = ThresholdKind.Score, Value = "990000" },
                            new() { Kind = ThresholdKind.Score, Value = "1000000" }
                        },
                        SeparateSinglesDoubles = false, MinLevel = 17, MaxLevel = 23
                    })),
                new WidgetDrawerPreset("Chart Age",
                    "How stale your scores are per folder — the spread of days since you set them.",
                    WidgetConfigJson.Write(new ByLevelBreakdownConfig
                    {
                        Metric = BreakdownMetric.ChartAge, Aggregation = BreakdownAggregation.Distribution,
                        Series = new List<DistributionSeries>
                        {
                            DistributionSeries.Min, DistributionSeries.P25, DistributionSeries.Median,
                            DistributionSeries.P75, DistributionSeries.Max
                        },
                        Band = BreakdownBand.InterQuartile, SeparateSinglesDoubles = false,
                        MinLevel = 17, MaxLevel = 23
                    }))
            },
            DynamicNameKey: configJson =>
            {
                var config = WidgetConfigJson.Read<ByLevelBreakdownConfig>(configJson);
                return ByLevelConfigRules.TitleKey(config.Metric, config.Aggregation);
            }),
        new("quick-record",
            "Quick Record",
            "Record a score by hand for any chart.",
            WidgetCategory.Play,
            Icons.Material.Filled.EditNote,
            // 1x1 only (owner): the one widget whose size list is a single entry.
            new[] { SizePreset.OneByOne },
            SizePreset.OneByOne,
            // Records to every mix (owner, 2026-07-13): Phoenix path for P1/P2, the legacy
            // letter-grade path for XX and older. "Follow current mix" honours any of them.
            Enum.GetValues<MixEnum>(),
            typeof(QuickRecordWidget),
            typeof(QuickRecordConfigPanel),
            typeof(QuickRecordConfig))
    };

    private static readonly IReadOnlyDictionary<string, WidgetDescriptor> ByTypeId =
        Descriptors.ToDictionary(d => d.TypeId, StringComparer.Ordinal);

    public static IReadOnlyList<WidgetDescriptor> All => Descriptors;

    public static WidgetDescriptor? TryGet(string typeId)
    {
        return ByTypeId.GetValueOrDefault(typeId);
    }
}
