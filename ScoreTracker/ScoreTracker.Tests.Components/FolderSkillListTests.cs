using System;
using System.Collections.Generic;
using System.Linq;
using Bunit;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     "Your Skills in Folder" — one row per BADGE (docs/design/chart-identity.md §7, the
///     owner's Variant B). The granular vocabulary is the point: "you are 16% on Twists" hides
///     which twists, and the whole reason to open the drawer is to find out.
/// </summary>
public sealed class FolderSkillListTests : ComponentTestBase
{
    private static Chart Chart(Guid id)
    {
        return new Chart(id, MixEnum.Phoenix,
            new Song(Name.From("Song"), SongType.Arcade, new Uri("https://piuscores.arroweclip.se/img.png"),
                TimeSpan.FromMinutes(2), "Artist", null),
            ChartType.Double, DifficultyLevel.From(24), MixEnum.Phoenix, null, 900);
    }

    private static ChartBadgePresenceRecord Badge(string badge, string display, BadgeCategory family,
        decimal weight)
    {
        return new ChartBadgePresenceRecord(badge, display, family, weight);
    }

    private static RecordedPhoenixScore Score(Guid chartId, int score)
    {
        return new RecordedPhoenixScore(chartId, PhoenixScore.From(score), null, false, DateTimeOffset.Now);
    }

    private IRenderedComponent<FolderSkillList> Render(
        IReadOnlyDictionary<Guid, IReadOnlyList<ChartBadgePresenceRecord>> presence,
        IDictionary<Guid, RecordedPhoenixScore> scores, IEnumerable<Chart> charts)
    {
        return RenderComponent<FolderSkillList>(p => p
            .Add(x => x.Charts, charts)
            .Add(x => x.BadgePresence, presence)
            .Add(x => x.Scores, scores));
    }

    /// <summary>
    ///     Two kinds of twist that the retired rollup averaged into one bar stay apart, so a
    ///     reader can see the one they are actually bad at.
    /// </summary>
    [Fact]
    public void EachBadgeGetsItsOwnRowRatherThanBeingAveragedIntoAFamily()
    {
        var weak = Guid.NewGuid();
        var strong = Guid.NewGuid();
        var presence = new Dictionary<Guid, IReadOnlyList<ChartBadgePresenceRecord>>
        {
            [weak] = new[]
            {
                Badge("twist_90", "Twist 90", BadgeCategory.Twists, 0.5m),
                Badge("twist_close", "Close Twists", BadgeCategory.Twists, 0.5m)
            },
            [strong] = new[]
            {
                Badge("twist_90", "Twist 90", BadgeCategory.Twists, 0.5m),
                Badge("twist_close", "Close Twists", BadgeCategory.Twists, 0.5m)
            }
        };
        var scores = new Dictionary<Guid, RecordedPhoenixScore>
        {
            [weak] = Score(weak, 910_000), [strong] = Score(strong, 990_000)
        };

        var cut = Render(presence, scores, new[] { Chart(weak), Chart(strong) });

        Assert.Contains("Twist 90", cut.Markup);
        Assert.Contains("Close Twists", cut.Markup);
        Assert.Equal(2, cut.FindAll(".tier-folder-skill-row").Count);
    }

    [Fact]
    public void RowsWearTheirBadgeFamilysTintAndTheWiderBadgeLayout()
    {
        var chartId = Guid.NewGuid();
        var presence = new Dictionary<Guid, IReadOnlyList<ChartBadgePresenceRecord>>
        {
            [chartId] = new[] { Badge("mid6_doubles", "Mid-6 Doubles", BadgeCategory.DoublesTech, 0.5m) }
        };
        var second = Guid.NewGuid();
        presence[second] = presence[chartId];

        var cut = Render(presence, new Dictionary<Guid, RecordedPhoenixScore>(),
            new[] { Chart(chartId), Chart(second) });

        Assert.Single(cut.FindAll(".badgecat-doublestech"));
        Assert.Single(cut.FindAll(".tier-folder-skill-row-badge"));
    }

    /// <summary>
    ///     Weakest first, where weak means both "you score badly on it" and "you have barely
    ///     touched it" — the product the owner's workshop settled on.
    /// </summary>
    [Fact]
    public void TheWeakestBadgeLeadsTheList()
    {
        var badTwist = Guid.NewGuid();
        var goodRun = Guid.NewGuid();
        var presence = new Dictionary<Guid, IReadOnlyList<ChartBadgePresenceRecord>>
        {
            [badTwist] = new[] { Badge("twist_90", "Twist 90", BadgeCategory.Twists, 1m) },
            [goodRun] = new[] { Badge("run", "Runs", BadgeCategory.StaminaAndRuns, 1m) }
        };
        var secondTwist = Guid.NewGuid();
        var secondRun = Guid.NewGuid();
        presence[secondTwist] = presence[badTwist];
        presence[secondRun] = presence[goodRun];
        var scores = new Dictionary<Guid, RecordedPhoenixScore>
        {
            [badTwist] = Score(badTwist, 905_000), [secondTwist] = Score(secondTwist, 905_000),
            [goodRun] = Score(goodRun, 995_000), [secondRun] = Score(secondRun, 995_000)
        };

        var cut = Render(presence, scores,
            new[] { Chart(badTwist), Chart(secondTwist), Chart(goodRun), Chart(secondRun) });

        var labels = cut.FindAll(".tier-folder-skill-label").Select(e => e.TextContent.Trim()).ToArray();
        Assert.Equal("Twist 90", labels[0]);
        Assert.Equal("Runs", labels[1]);
    }

    /// <summary>One chart is not a reading — a badge needs company before it earns a row.</summary>
    [Fact]
    public void ABadgeOnASingleChartIsTooLittleToRow()
    {
        var chartId = Guid.NewGuid();
        var presence = new Dictionary<Guid, IReadOnlyList<ChartBadgePresenceRecord>>
        {
            [chartId] = new[] { Badge("split", "Splits", BadgeCategory.DoublesTech, 0.5m) }
        };

        var cut = Render(presence, new Dictionary<Guid, RecordedPhoenixScore>(), new[] { Chart(chartId) });

        Assert.Empty(cut.FindAll(".tier-folder-skill-row"));
    }

    [Fact]
    public void AFolderWithNoBankedAnalysisRendersNothingAtAll()
    {
        var cut = Render(new Dictionary<Guid, IReadOnlyList<ChartBadgePresenceRecord>>(),
            new Dictionary<Guid, RecordedPhoenixScore>(), new[] { Chart(Guid.NewGuid()) });

        Assert.Empty(cut.Markup.Trim());
    }
}
