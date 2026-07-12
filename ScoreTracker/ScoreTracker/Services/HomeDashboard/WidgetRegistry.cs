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
        new("suggested-charts",
            "Suggested Charts",
            "Charts picked for you, tuned by goal.",
            WidgetCategory.Play,
            Icons.Material.Filled.Recommend,
            new[] { SizePreset.OneByTwo, SizePreset.TwoByOne, SizePreset.TwoByTwo },
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
                        Spread = 3
                    }))
            })
    };

    private static readonly IReadOnlyDictionary<string, WidgetDescriptor> ByTypeId =
        Descriptors.ToDictionary(d => d.TypeId, StringComparer.Ordinal);

    public static IReadOnlyList<WidgetDescriptor> All => Descriptors;

    public static WidgetDescriptor? TryGet(string typeId)
    {
        return ByTypeId.GetValueOrDefault(typeId);
    }
}
