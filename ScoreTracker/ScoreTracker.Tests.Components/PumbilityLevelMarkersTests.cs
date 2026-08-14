using System.Linq;
using Bunit;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The one formula behind every level-ticked bar, plus the shared tick fragment. Every
///     pumbility bar spans exactly one gem (LinkLadder floors each rung at the rung below), so
///     what matters is that the ticks land evenly, the current one is marked, and the spans that
///     hold no rungs — BRONZE's first rung, non-pumbility titles — yield exactly nothing.
/// </summary>
public sealed class PumbilityLevelMarkersTests : ComponentTestBase
{
    [Fact]
    public void AOneGemSpanTicksItsFourInteriorRungsEvenly()
    {
        var ticks = PumbilityLevelMarkers.TicksFor(17_000, 18_000, 17_602.69);

        Assert.Equal(new[] { 0.2, 0.4, 0.6, 0.8 }, ticks.Select(t => t.Fraction));
        Assert.Equal(new int?[] { 2, 3, 4, 5 }, ticks.Select(t => t.Level.Level));
        // 17,602.69 stands on DIAMOND LV.4 — that tick and only that tick reads current.
        Assert.Equal(new[] { false, false, true, false }, ticks.Select(t => t.Current));
    }

    [Fact]
    public void TheDrawersFloorToThresholdSpanTicksTheGemBelow()
    {
        // The bar toward [P.B] DIAMOND runs 16,000 → 17,000: PLATINUM's levels, the rungs being
        // climbed through on the way to the title.
        var ticks = PumbilityLevelMarkers.TicksFor(16_000, 17_000, 16_450);

        Assert.Equal(new[] { "[P.B] PLATINUM" }, ticks.Select(t => t.Level.Gem!.Value.ToString()).Distinct());
        // 16,450 stands on PLATINUM LV.3 (16,400+).
        Assert.Equal(3, Assert.Single(ticks, t => t.Current).Level.Level);
    }

    [Fact]
    public void TheSpanEndsAreNeverTicked()
    {
        // 17,000 and 18,000 are the bar's own endpoints — a tick there would double the edge.
        var ticks = PumbilityLevelMarkers.TicksFor(17_000, 18_000, 17_100);

        Assert.DoesNotContain(ticks, t => t.Level.Threshold is 17_000 or 18_000);
    }

    [Fact]
    public void SpansHoldingNoRungsYieldNothing()
    {
        // BRONZE's first rung: 0 → 10,000 contains no levels at all.
        Assert.Empty(PumbilityLevelMarkers.TicksFor(0, 10_000, 4_000));
        // A degenerate span cannot divide.
        Assert.Empty(PumbilityLevelMarkers.TicksFor(17_000, 17_000, 17_000));
        Assert.Empty(PumbilityLevelMarkers.TicksFor(18_000, 17_000, 17_500));
    }

    [Fact]
    public void APoolOutsideTheSpanMarksNothingCurrent()
    {
        // An earned rung's bar renders full with its ticks intact — but the pool stands above
        // every tick in the span, so none of them claims to be where the player is.
        var ticks = PumbilityLevelMarkers.TicksFor(16_000, 17_000, 17_602.69);

        Assert.Equal(4, ticks.Count);
        Assert.DoesNotContain(ticks, t => t.Current);
    }

    [Fact]
    public void TheCapstoneBarTicksAlexandritesLevels()
    {
        var ticks = PumbilityLevelMarkers.TicksFor(19_000, 20_000, 19_616.11);

        Assert.Equal(new[] { 19_200, 19_400, 19_600, 19_800 }, ticks.Select(t => t.Level.Threshold));
        Assert.Equal(4, Assert.Single(ticks, t => t.Current).Level.Level);
    }

    [Fact]
    public void GemSpansResolveByNameAndOnlyForTotalPoolGems()
    {
        Assert.Equal((16_000, 17_000), PumbilityLevelMarkers.GemSpanFor("[P.B] DIAMOND"));
        Assert.Equal((0, 10_000), PumbilityLevelMarkers.GemSpanFor("[P.B] BRONZE"));
        Assert.Equal((19_000, 20_000), PumbilityLevelMarkers.GemSpanFor("ABYSS ABSOLUTE"));

        // The per-type ladders have no levels, and Phoenix has no gem ladder — the null is the
        // gate that keeps those bars exactly as they are.
        Assert.Null(PumbilityLevelMarkers.GemSpanFor("[S] ADVANCED LV.2"));
        Assert.Null(PumbilityLevelMarkers.GemSpanFor("SINGLE MASTER"));
        Assert.Null(PumbilityLevelMarkers.GemSpanFor("Advanced Lv. 3"));
    }

    [Fact]
    public void TheTickFragmentRendersPositionsCurrentAndHoverNames()
    {
        var cut = RenderComponent<PumbilityLevelTicks>(p => p
            .Add(x => x.Floor, 17_000).Add(x => x.Ceiling, 18_000).Add(x => x.Pool, 17_602.69));

        var ticks = cut.FindAll(".pmb-lvl-tick");
        Assert.Equal(4, ticks.Count);
        Assert.Equal(new[] { "left:20%", "left:40%", "left:60%", "left:80%" },
            ticks.Select(t => t.GetAttribute("style")));
        Assert.Contains("cur", Assert.Single(ticks, t => t.GetAttribute("title") == "DIAMOND LV.4 · 17,600")
            .GetAttribute("class"));
    }

    [Fact]
    public void TheTickFragmentRendersNothingForABareSpan()
    {
        var cut = RenderComponent<PumbilityLevelTicks>(p => p
            .Add(x => x.Floor, 0).Add(x => x.Ceiling, 10_000).Add(x => x.Pool, 4_000));

        Assert.Empty(cut.Markup.Trim());
    }
}
