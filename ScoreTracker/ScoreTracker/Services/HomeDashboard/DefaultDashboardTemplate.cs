using ScoreTracker.Web.Components.HomeWidgets;

namespace ScoreTracker.Web.Services.HomeDashboard;

/// <summary>
///     The curated default dashboard a brand-new user gets from "Create" (the cutover
///     default, docs/design/HomePageWidgets/README.md D1). Every widget follows the
///     current mix — nothing pins a Mix, and the page's DefaultMix stays null.
///     <para>
///     Order is load-bearing: the grid is 4 columns, <c>grid-auto-flow: row</c>
///     (non-dense), so widgets pack in this exact sequence and gaps never backfill.
///     The eight tile into a 4×4 block plus a full-width footer strip:
///     </para>
///     <code>
///     row 1: [Account Stats 1x2][Suggested·Pumbility 1x2][Import 1x1 ][Daily 1x2 ]
///     row 2: [    (cont.)      ][       (cont.)         ][Community  ][ (cont.)  ]
///     row 3: [Folder Completion 2x2                     ][ 1x3       ][Weekly 1x2]
///     row 4: [       (cont.)                            ][ (cont.)   ][ (cont.)  ]
///     row 5: [Suggested·Title Hunt — full width 4x1                             ]
///     </code>
///     Folder must precede Weekly: non-dense flow would otherwise let Weekly claim the
///     open bottom-left 2×2 slot before Folder reaches it.
/// </summary>
public static class DefaultDashboardTemplate
{
    /// <summary>One seeded widget: registry TypeId, size token, and its config blob.</summary>
    public sealed record Entry(string TypeId, string SizeToken, string ConfigJson);

    public static IReadOnlyList<Entry> Entries { get; } = Build();

    private static IReadOnlyList<Entry> Build()
    {
        return new[]
        {
            new Entry("pumbility", "1x2", "{}"),
            new Entry("suggested-charts", "1x2",
                WidgetConfigJson.Write(new SuggestedChartsConfig { Goal = SuggestedGoal.PumbilityPush })),
            new Entry("import-scores", "1x1", "{}"),
            new Entry("daily-step", "1x2", "{}"),
            new Entry("community-highlights", "1x3", "{}"),
            new Entry("by-level-breakdown", "2x2",
                WidgetConfigJson.Write(new ByLevelBreakdownConfig
                {
                    Metric = BreakdownMetric.Pass,
                    Aggregation = BreakdownAggregation.Breakdown,
                    // Whole folder, both types together — "how much of every level have I cleared".
                    SeparateSinglesDoubles = false,
                    MinLevel = 1,
                    MaxLevel = 29
                })),
            new Entry("weekly-challenge", "1x2", "{}"),
            new Entry("suggested-charts", "4x1",
                WidgetConfigJson.Write(new SuggestedChartsConfig { Goal = SuggestedGoal.TitleHunt }))
        };
    }
}
