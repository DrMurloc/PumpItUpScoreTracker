using System;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class TierListChartCardTests : ComponentTestBase
{
    // The card nests DifficultyBubble, which gates its tooltip on RendererInfo.IsInteractive
    // and throws when bUnit leaves that unset.
    public TierListChartCardTests()
    {
        this.RenderInteractive();
    }

    [Fact]
    public void TheProjectedLineNamesWhoseNumberItIs()
    {
        // A bare score beside the player's own reads as a target somebody set for them.
        var cut = Render(showProjected: true, projected: 962_400);

        Assert.Contains("962,400", cut.Markup);
        Assert.Contains("projected", cut.Markup);
    }

    [Fact]
    public void AChartNobodyAtYourLevelHasPlayedSaysSoRatherThanGoingBlank()
    {
        // The absence has to be stated. A line that simply vanishes reads as the chart having
        // no data at all, and a zero would be a number nobody produced.
        var cut = Render(showProjected: true, projected: null);

        Assert.Contains("projected", cut.Markup);
        Assert.DoesNotContain("0", cut.Find(".tier-chart-card-meta").TextContent);
    }

    [Fact]
    public void TheLineStaysOffWhenTheSwitchIsOff()
    {
        var cut = Render(showProjected: false, projected: 962_400);

        Assert.DoesNotContain("projected", cut.Markup);
        Assert.DoesNotContain("962,400", cut.Markup);
    }

    [Fact]
    public void TheStandingPrintsInTheBodyBesideTheColorItExplains()
    {
        var cut = RenderScored(new PeerStanding(93, 93, 5, 0, 0, Array.Empty<PeerStandingSource>(), null));

        Assert.Contains("#6 of 94 peers", cut.Find(".tier-chart-card-standing").TextContent);
        Assert.Contains("--rarity-sapphire", cut.Find("[data-testid='peer-score']").GetAttribute("style"));
    }

    [Fact]
    public async Task TappingTheScoreDoesNotOpenDetails()
    {
        // The head strip opens details and the score sits inside it; the score stops the tap so
        // the standing popover, not the dialog, is what a tap on the number gets (D17).
        var opened = 0;
        var cut = RenderScored(new PeerStanding(93, 93, 5, 0, 0, Array.Empty<PeerStandingSource>(), null),
            onOpen: EventCallback.Factory.Create<Guid>(this, _ => opened++));

        await cut.Find("[data-testid='peer-score']").ClickAsync(new MouseEventArgs());

        Assert.Equal(0, opened);
        Assert.Equal("true", cut.Find("[data-testid='peer-score']").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void APrintedValueSharesTheNamesLineSoALongNameStopsWhereTheValueStarts()
    {
        // The value takes the end of the name's line, not the jacket corner, so the name is what
        // gives way when the two do not fit side by side.
        var cut = RenderComponent<TierListChartCard>(p => p
            .Add(x => x.Chart, ProbeChart())
            .Add(x => x.CornerBadge, "+18")
            .Add(x => x.CornerBadgeClass, "pmb-corner-gain"));

        var line = cut.Find(".tier-chart-card-head .tier-chart-card-nameline");
        Assert.Equal("Sarabande", line.QuerySelector(".tier-chart-card-name")!.TextContent);
        var value = line.QuerySelector(".tier-chart-card-corner")!;
        Assert.Equal("+18", value.TextContent.Trim());
        Assert.Contains("pmb-corner-gain", value.ClassName);
        Assert.Empty(cut.FindAll(".tier-chart-card-jacket .tier-chart-card-corner"));
    }

    [Fact]
    public void TheJacketOpensTheChartAndNothingElse()
    {
        // The jacket USED to carry a full-bleed play button (inset: 0) that opened the details
        // dialog with the video autoplaying, so clicking the artwork played a song instead of
        // opening the chart, and hovering it put a glyph over the art. Both gone (owner,
        // 2026-09-12) - the jacket is the way in, and the only way in it offers.
        var cut = RenderScored(null);

        Assert.DoesNotContain("tier-chart-card-play", cut.Markup);
        Assert.DoesNotContain("jacket-playable", cut.Markup);
    }

    [Fact]
    public void TheNameComesBeforeTheScoreInTheReadingOrder()
    {
        // The name is lifted out of the head's flow by CSS so it cannot push the score off the
        // jacket's edge, but it must still be READ first: it says what the card is about, and a
        // screen reader takes the DOM order, not the painted one.
        var cut = RenderComponent<TierListChartCard>(p => p
            .Add(x => x.Chart, ProbeChart())
            .Add(x => x.ShowName, true)
            .Add(x => x.Score, new RecordedPhoenixScore(Guid.NewGuid(), PhoenixScore.From(972_000), null,
                false, new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero))));

        var head = cut.Find(".tier-chart-card-head").InnerHtml;
        var name = head.IndexOf("tier-chart-card-nameline", StringComparison.Ordinal);
        var score = head.IndexOf("tier-chart-card-scoreline", StringComparison.Ordinal);
        Assert.True(name >= 0 && score >= 0, "the head carries both lines");
        Assert.True(name < score, "the name is read before the score");
    }

    [Fact]
    public void ToDoOutranksAPassOnTheBorder()
    {
        // Every other border state REPORTS something about the chart; To-Do is the one the
        // player put there (owner, 2026-09-12: it "overwrites other boarder colors"). A flag you
        // cannot see on a chart you have already passed is a flag that does not work.
        var cut = RenderComponent<TierListChartCard>(p => p
            .Add(x => x.Chart, ProbeChart())
            .Add(x => x.Passed, true)
            .Add(x => x.IsToDo, true));

        Assert.Contains("tier-chart-card-todo", cut.Markup);
        Assert.DoesNotContain("tier-chart-card-pass", cut.Markup);
    }

    [Fact]
    public void ToDoOutranksACustomStateBorderToo()
    {
        // The Hardmode pool paints its fifty gold through CustomStateClass. A To-Do on one of
        // those still has to read as a To-Do.
        var cut = RenderComponent<TierListChartCard>(p => p
            .Add(x => x.Chart, ProbeChart())
            .Add(x => x.CustomStateClass, "tier-chart-card-top50")
            .Add(x => x.IsToDo, true));

        Assert.Contains("tier-chart-card-todo", cut.Markup);
        Assert.DoesNotContain("tier-chart-card-top50", cut.Markup);
    }

    [Fact]
    public void ACustomStateStillWinsWhenNothingIsFlagged()
    {
        var cut = RenderComponent<TierListChartCard>(p => p
            .Add(x => x.Chart, ProbeChart())
            .Add(x => x.CustomStateClass, "tier-chart-card-top50")
            .Add(x => x.Passed, true));

        Assert.Contains("tier-chart-card-top50", cut.Markup);
        Assert.DoesNotContain("tier-chart-card-pass", cut.Markup);
    }

    private IRenderedComponent<TierListChartCard> RenderScored(PeerStanding? standing,
        EventCallback<Guid> onOpen = default)
    {
        var chart = ProbeChart();
        var score = new RecordedPhoenixScore(chart.Id, PhoenixScore.From(972_000), null, false,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));

        return RenderComponent<TierListChartCard>(p => p
            .Add(x => x.Chart, chart)
            .Add(x => x.Score, score)
            .Add(x => x.Standing, standing)
            .Add(x => x.OnOpen, onOpen));
    }

    private IRenderedComponent<TierListChartCard> Render(bool showProjected, int? projected)
    {
        return RenderComponent<TierListChartCard>(p => p
            .Add(x => x.Chart, ProbeChart())
            .Add(x => x.ShowProjectedScore, showProjected)
            .Add(x => x.ProjectedScore, projected == null ? null : PhoenixScore.From(projected.Value)));
    }

    private static Chart ProbeChart() =>
        new(Guid.NewGuid(), MixEnum.Phoenix,
            new Song(Name.From("Sarabande"), SongType.Arcade,
                new Uri("https://piuimages.arroweclip.se/probe.png"), TimeSpan.Zero,
                Name.From("Probe"), null),
            ChartType.Double, DifficultyLevel.From(18), MixEnum.Phoenix, null, null);
}
