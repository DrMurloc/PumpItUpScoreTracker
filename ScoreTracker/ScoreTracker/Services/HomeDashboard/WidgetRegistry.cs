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
            typeof(CompetitiveLevelConfig),
            // History rows are written by the post-batch analysis, not the import itself —
            // an import-time reload would refetch before the new point exists.
            RefreshOnStatsUpdate: true),
        // TypeId stays "pumbility" (public export vocabulary, never renamed) though the
        // widget is now Account Stats. 1x2+ adds the closest-competitive-matches list.
        new("pumbility",
            "Account Stats",
            "Your Pumbility and competitive level, plus your closest matches.",
            WidgetCategory.Progress,
            Icons.Material.Filled.Leaderboard,
            new[] { SizePreset.OneByOne, SizePreset.OneByTwo, SizePreset.OneByThree },
            SizePreset.OneByTwo,
            new[] { MixEnum.Phoenix, MixEnum.Phoenix2 },
            typeof(PumbilityWidget),
            typeof(PumbilityConfigPanel),
            typeof(PumbilityConfig),
            // Everything here is the stored PlayerStats record — it only changes when the
            // post-batch analysis lands, so that is the refresh signal.
            RefreshOnStatsUpdate: true),
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
            typeof(WeeklyConfig),
            RefreshOnScoreImport: true),
        // The KEY stays "community-highlights" forever: it is persisted on every widget instance
        // that already exists, and changing it would orphan every dashboard carrying one. Only the
        // label moved, because the widget now mixes two audiences (docs/design/rivals.md D38).
        new("community-highlights",
            "Highlights feed",
            "Recent big wins from the communities you pick, and optionally your rivals.",
            WidgetCategory.Compete,
            Icons.Material.Filled.Groups,
            new[]
            {
                SizePreset.OneByTwo, SizePreset.TwoByOne, SizePreset.ThreeByOne, SizePreset.FourByOne,
                SizePreset.TwoByTwo, SizePreset.OneByThree
            },
            SizePreset.TwoByTwo,
            new[] { MixEnum.Phoenix, MixEnum.Phoenix2 },
            typeof(CommunityHighlightsWidget),
            typeof(CommunityHighlightsConfigPanel),
            typeof(CommunityHighlightsConfig),
            // One widget type, two audiences, two drawer cards (D10). "Rivals feed" is not a
            // second widget — it is this one with the community half switched off — but nobody
            // finds a feature by adding a widget called something else and unticking a box.
            DrawerPresets: new[]
            {
                new WidgetDrawerPreset("Highlights feed",
                    "Recent big wins from the communities you pick.",
                    WidgetConfigJson.Write(new CommunityHighlightsConfig())),
                new WidgetDrawerPreset("Rivals feed",
                    "Recent big wins from the players you rival.",
                    WidgetConfigJson.Write(new CommunityHighlightsConfig
                    {
                        IncludeCommunities = false,
                        IncludeRivals = true
                    }))
            },
            // So the two presets wear their own titles without storing one per instance.
            DynamicNameKey: json =>
            {
                var config = WidgetConfigJson.Read<CommunityHighlightsConfig>(json);
                return config is { IncludeCommunities: false, IncludeRivals: true }
                    ? "Rivals feed"
                    : null;
            }),
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
            typeof(DailyStepConfig),
            RefreshOnScoreImport: true),
        new("folder-levels",
            "Folder Levels",
            "How far through your folders you are, and how well.",
            WidgetCategory.Progress,
            Icons.Material.Filled.Folder,
            // Each size holds a fixed number of folders (FolderLevelsDefaults.CapacityFor), and
            // detail drops with the cell rather than the layout changing: 1x1 is one folder
            // large, everything else stacks rows, and the tier ticks need a second row to be
            // worth cutting in.
            new[]
            {
                SizePreset.OneByOne, SizePreset.TwoByOne, SizePreset.TwoByTwo, SizePreset.TwoByThree
            },
            SizePreset.TwoByTwo,
            new[] { MixEnum.Phoenix, MixEnum.Phoenix2 },
            typeof(FolderLevelsWidget),
            typeof(FolderLevelsConfigPanel),
            typeof(FolderLevelsConfig),
            RefreshOnScoreImport: true),
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
                    })),
                new WidgetDrawerPreset("Suggested · Pumbility Push",
                    "The biggest Pumbility gains available to you right now.",
                    WidgetConfigJson.Write(new SuggestedChartsConfig { Goal = SuggestedGoal.PumbilityPush })),
                new WidgetDrawerPreset("Suggested · Hot Streak",
                    "More charts like the recent plays you crushed against your Peers.",
                    WidgetConfigJson.Write(new SuggestedChartsConfig { Goal = SuggestedGoal.HotStreak }))
            },
            // Instance titles follow the configured goal so rapid-firing all three
            // presets never yields three "Suggested Charts" (owner, field test).
            DynamicNameKey: configJson =>
                WidgetConfigJson.Read<SuggestedChartsConfig>(configJson).Goal switch
                {
                    SuggestedGoal.ScorePush => "Suggested · Score Push",
                    SuggestedGoal.FillGaps => "Suggested · Fill Gaps",
                    SuggestedGoal.PumbilityPush => "Suggested · Pumbility Push",
                    SuggestedGoal.HotStreak => "Suggested · Hot Streak",
                    _ => "Suggested · Title Hunt"
                },
            RefreshIcon: Icons.Material.Filled.Shuffle,
            RefreshTitleKey: "Shuffle suggestions",
            // Shuffle only where a re-roll changes the hand: Hot Streak is deterministic
            // (seeds newest-first, targets by similarity), so the button would lie there.
            // Pumbility Push samples its pool, so it keeps it — and the Title Hunt fallback
            // inherits that sampling on Phoenix 2.
            ShowRefresh: json =>
                WidgetConfigJson.Read<SuggestedChartsConfig>(json).Goal != SuggestedGoal.HotStreak,
            // The straddler: most goals read the scores (import-time), but Pumbility Push
            // gains ride the projections the post-batch analysis feeds — so both signals.
            RefreshOnScoreImport: true,
            RefreshOnStatsUpdate: true),
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
                        // Combined so the full box plot reads; Singles vs Doubles is the separate cut.
                        Band = BreakdownBand.InterQuartile, SeparateSinglesDoubles = false,
                        MinLevel = 17, MaxLevel = 23
                    })),
                new WidgetDrawerPreset("Singles vs Doubles",
                    "Your score spread per level, Singles against Doubles, the middle 50% shaded.",
                    WidgetConfigJson.Write(new ByLevelBreakdownConfig
                    {
                        Metric = BreakdownMetric.Score, Aggregation = BreakdownAggregation.Distribution,
                        SeparateSinglesDoubles = true, SeparateDisplay = SeparateDisplay.RangeIqr,
                        MinLevel = 17, MaxLevel = 23
                    })),
                new WidgetDrawerPreset("Grade Distribution",
                    "Every grade stacked per folder, with a broken / unplayed cap for the folder count.",
                    WidgetConfigJson.Write(new ByLevelBreakdownConfig
                    {
                        Metric = BreakdownMetric.LetterGrade, Aggregation = BreakdownAggregation.Breakdown,
                        Normalize = false, IncludeUnplayed = true, SeparateSinglesDoubles = false,
                        MinLevel = 17, MaxLevel = 24
                    })),
                new WidgetDrawerPreset("Plate Distribution",
                    "Every plate stacked per folder, with a broken / unplayed cap for the folder count.",
                    WidgetConfigJson.Write(new ByLevelBreakdownConfig
                    {
                        Metric = BreakdownMetric.Plate, Aggregation = BreakdownAggregation.Breakdown,
                        Normalize = false, IncludeUnplayed = true, SeparateSinglesDoubles = false,
                        MinLevel = 17, MaxLevel = 23
                    })),
                new WidgetDrawerPreset("Clear Progress",
                    "How much of every folder you've cleared, per level.",
                    WidgetConfigJson.Write(new ByLevelBreakdownConfig
                    {
                        Metric = BreakdownMetric.Pass, Aggregation = BreakdownAggregation.Breakdown,
                        SeparateSinglesDoubles = false, MinLevel = 1, MaxLevel = 28
                    })),
                // Clear Progress coloured by grade — the folder-levels spectrum at all-folders
                // scale (docs/design/folder-level-progression.md §6). Deliberately a preset
                // rather than a colour field on Clear Progress: the LetterGrade metric already
                // expresses it exactly, and a second way to say the same thing would widen the
                // public config vocabulary (D19) for nothing.
                new WidgetDrawerPreset("Clear Progress by Grade",
                    "How much of every folder you've cleared, coloured by the grades you hold.",
                    WidgetConfigJson.Write(new ByLevelBreakdownConfig
                    {
                        Metric = BreakdownMetric.LetterGrade, Aggregation = BreakdownAggregation.Breakdown,
                        Normalize = true, IncludeUnplayed = true, SeparateSinglesDoubles = false,
                        MinLevel = 1, MaxLevel = 28
                    })),
                new WidgetDrawerPreset("Co-Op Completion",
                    "How many co-op charts you've cleared, by player count.",
                    WidgetConfigJson.Write(new ByLevelBreakdownConfig
                    {
                        Scope = BreakdownChartScope.CoOp,
                        Metric = BreakdownMetric.Pass, Aggregation = BreakdownAggregation.Breakdown,
                        MinPlayers = 2, MaxPlayers = 5
                    }))
            },
            DynamicNameKey: configJson =>
            {
                var config = WidgetConfigJson.Read<ByLevelBreakdownConfig>(configJson);
                // Two configs wear their own name instead of the generic metric/aggregation title.
                if (config.Scope == BreakdownChartScope.SinglesDoubles && config.SeparateSinglesDoubles
                    && config.Metric == BreakdownMetric.Score
                    && config.Aggregation == BreakdownAggregation.Distribution)
                    return "Singles vs Doubles";
                if (config.Scope == BreakdownChartScope.CoOp && config.Metric == BreakdownMetric.Pass)
                    return "Co-Op Completion";
                return ByLevelConfigRules.TitleKey(config.Metric, config.Aggregation);
            },
            RefreshOnScoreImport: true),
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
            typeof(QuickRecordConfig)),
        new("import-scores",
            "Import Scores",
            "Import your scores from piugame.com — remembered, one tap, in the background.",
            WidgetCategory.Utility,
            Icons.Material.Filled.CloudDownload,
            // 1x1 only, like Quick Record.
            new[] { SizePreset.OneByOne },
            SizePreset.OneByOne,
            // Every mix: Phoenix 1/2 import with credentials, XX and older via spreadsheet upload.
            Enum.GetValues<MixEnum>(),
            typeof(ImportScoresWidget),
            typeof(ImportScoresConfigPanel),
            typeof(ImportScoresConfig))
    };

    private static readonly IReadOnlyDictionary<string, WidgetDescriptor> ByTypeId =
        Descriptors.ToDictionary(d => d.TypeId, StringComparer.Ordinal);

    public static IReadOnlyList<WidgetDescriptor> All => Descriptors;

    public static WidgetDescriptor? TryGet(string typeId)
    {
        return ByTypeId.GetValueOrDefault(typeId);
    }
}
