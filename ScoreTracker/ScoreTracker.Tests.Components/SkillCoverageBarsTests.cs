using System;
using System.Collections.Generic;
using Bunit;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The coverage bars shared by the chart page and its dialog. They read the granular
///     piucenter badges — the vocabulary the similar-charts algorithm compares on — not the
///     Skill rollup that collapsed 33 badges into 11 arbitrary buckets
///     (docs/design/nuke-old-skill-categories.md).
/// </summary>
public sealed class SkillCoverageBarsTests : ComponentTestBase
{
    private IRenderedComponent<SkillCoverageBars> Render(params ChartBadgeChipRecord[] chips)
    {
        return RenderComponent<SkillCoverageBars>(p => p
            .Add(x => x.Chips, (IReadOnlyList<ChartBadgeChipRecord>)chips));
    }

    [Fact]
    public void EachBadgeKeepsItsOwnNameAndItsMeasuredCoverage()
    {
        // Two twists that the rollup would have averaged into one "Twists" bar, and a
        // bracket variety it would have merged with plain brackets.
        var cut = Render(
            new ChartBadgeChipRecord("twist_over90", "Over-90 Twists", BadgeCategory.Twists, true, 0.42m),
            new ChartBadgeChipRecord("twist_close", "Close Twists", BadgeCategory.Twists, true, 0.11m),
            new ChartBadgeChipRecord("staggered_bracket", "Staggered Brackets", BadgeCategory.Brackets, false, 0.30m));

        var bars = cut.FindAll(".chart-details-skill-bar");
        Assert.Equal(3, bars.Count);
        Assert.Contains("Over-90 Twists", cut.Markup);
        Assert.Contains("Close Twists", cut.Markup);
        Assert.Contains("42%", cut.Markup);
        Assert.Contains("11%", cut.Markup);
    }

    [Fact]
    public void EachBarWearsItsFamilysTintAndNoneOfTheRollupsBuckets()
    {
        var cut = Render(
            new ChartBadgeChipRecord("hands", "Hands", BadgeCategory.Tech, true, 0.2m),
            new ChartBadgeChipRecord("mid6_doubles", "Mid-6 Doubles", BadgeCategory.DoublesTech, true, 0.4m));

        Assert.Single(cut.FindAll(".badgecat-tech"));
        Assert.Single(cut.FindAll(".badgecat-doublestech"));
        // The retired rollup's buckets must not come back through a side door.
        Assert.Empty(cut.FindAll("[class*=skillcat-]"));
    }

    [Fact]
    public void ABadgeWithNoKnownFamilyStillRendersUntinted()
    {
        // piucenter can add vocabulary faster than the category table learns it; an unplaced
        // badge is still worth showing.
        var cut = Render(new ChartBadgeChipRecord("quad_anchor-stomp", "Quad Anchor Stomp", null, true, 0.1m));

        Assert.Contains("Quad Anchor Stomp", cut.Markup);
        Assert.Empty(cut.FindAll("[class*=badgecat-]"));
    }

    [Fact]
    public void AWholeChartQualityFillsTheBarAndPrintsNoNumber()
    {
        // bursty and sustained are intensity facts, not segment coverage: a null must read as
        // "true of this chart", never as zero percent.
        var cut = Render(new ChartBadgeChipRecord("sustained", "Sustained", BadgeCategory.StaminaAndRuns, true, null));

        Assert.Contains("width:100%", cut.Markup);
        Assert.Equal(string.Empty, cut.Find(".chart-details-skill-bar-value").TextContent.Trim());
    }
}
