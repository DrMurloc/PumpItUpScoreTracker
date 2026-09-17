using System;
using System.Collections.Generic;
using System.Linq;
using Bunit;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The PUMBILITY presence graph (docs/design/chart-presence-graph.md): the sentences over it, the titles-down
///     rows every host draws, the titles-across plot only the chart page may turn to, and what each title's
///     tooltip says.
/// </summary>
public sealed class ChartPumbilityPresenceTests : ComponentTestBase
{
    private static readonly Chart D23 = new(Guid.NewGuid(), MixEnum.Phoenix2,
        new Song(Name.From("Presence"), SongType.FullSong, new Uri("https://example.invalid/x.png"),
            TimeSpan.FromMinutes(2), Name.From("msgoon"), null),
        ChartType.Double, 23, MixEnum.Phoenix2, null, null);

    [Fact]
    public void NothingDrawsWithoutACensusReading()
    {
        var cut = Render(null, true);

        Assert.Empty(cut.FindAll("[data-testid=chart-presence]"));
    }

    [Fact]
    public void TheHeadlineNamesTheMostHeldTitleItsShareAndWhereItTypicallySits()
    {
        var cut = Render(Presence(1));

        Assert.Equal("Most held at DIAMOND LV.1: 40% hold it, typically around #13 in their top 50.",
            cut.Find("[data-testid=presence-headline]").TextContent);
    }

    [Fact]
    public void AMostHeldTitleWithTooFewHoldersForABoxNamesEachHoldersSpot()
    {
        var cut = Render(Presence(0));

        Assert.Equal("Most held at OPAL: 10% hold it, at #4, #9 and #20 in their top 50.",
            cut.Find("[data-testid=presence-headline]").TextContent);
    }

    [Fact]
    public void AChartNobodyHoldsSaysSoAndHasNoRatingOrYouLine()
    {
        var cut = Render(Presence(null));

        Assert.Equal("Nobody on the PUMBILITY ladder holds this chart in their top 50 yet.",
            cut.Find("[data-testid=presence-headline]").TextContent);
        Assert.Empty(cut.FindAll("[data-testid=presence-rating]"));
        Assert.Empty(cut.FindAll("[data-testid=presence-you]"));
    }

    [Fact]
    public void TheRatingLineReadsEachRunInLadderOrderAgainstTheChartsOwnFolder()
    {
        var cut = Render(Presence(1) with
        {
            Ratings = new[]
            {
                new PumbilityPresenceRun(PumbilityPresenceRating.Higher,
                    new[] { Name.From("[P.B] OPAL"), Name.From("[P.B] DIAMOND") }),
                new PumbilityPresenceRun(PumbilityPresenceRating.Same, new[] { Name.From("[P.B] RED BERYL") }),
                new PumbilityPresenceRun(PumbilityPresenceRating.Lower, new[] { Name.From("[P.B] ALEXANDRITE") })
            }
        });

        Assert.Equal(
            "Rates higher than other D23s at OPAL and DIAMOND, about the same at RED BERYL, lower at ALEXANDRITE.",
            cut.Find("[data-testid=presence-rating]").TextContent);
    }

    [Fact]
    public void YourLineSaysWhereItSitsInYourTopFiftyAndYourRowCarriesIt()
    {
        var cut = Render(Presence(1) with { Viewer = new PumbilityPresenceViewer(1, 7) });

        Assert.Equal("You're DIAMOND LV.1: it's #7 in your top 50.", cut.Find("[data-testid=presence-you]").TextContent);
        var rows = cut.FindAll("[data-testid=presence-row]");
        Assert.Contains("is-you", rows[1].ClassName);
        Assert.NotNull(rows[1].QuerySelector(".presence-row-level .presence-you-mark"));
        var tip = rows[1].QuerySelectorAll(".presence-tip > *").Last();
        Assert.Equal("You: #7 in your top 50", tip.TextContent);
        Assert.Equal("presence-tip-you", tip.ClassName);
        Assert.DoesNotContain("is-you", rows[0].ClassName);
        Assert.Null(rows[0].QuerySelector(".presence-you-mark"));
    }

    [Fact]
    public void YourColumnCarriesAFlagAboveItWhenTitlesRunAcross()
    {
        var cut = Render(Presence(1) with { Viewer = new PumbilityPresenceViewer(2, null) }, true);

        var flag = cut.Find(".presence-flag-you");
        Assert.Equal("grid-column:4", flag.GetAttribute("style"));
        Assert.Equal("You", flag.TextContent);
        Assert.Contains("is-you", cut.FindAll("[data-testid=presence-col]")[2].ClassName);
    }

    [Fact]
    public void NoFlagWithoutAViewer()
    {
        var cut = Render(Presence(1), true);

        Assert.Empty(cut.FindAll(".presence-flag"));
    }

    [Fact]
    public void YourLineSaysSoWhenTheChartIsNotInYourTopFifty()
    {
        var cut = Render(Presence(1) with { Viewer = new PumbilityPresenceViewer(1, null) });

        Assert.Equal("You're DIAMOND LV.1: it isn't in your top 50.", cut.Find("[data-testid=presence-you]").TextContent);
        Assert.Equal("Not in your top 50", Lines(cut.FindAll("[data-testid=presence-row]")[1]).Last());
    }

    [Fact]
    public void TheDialogDrawsTitlesDownAndNeverRendersTheAcrossPlot()
    {
        var cut = Render(Presence(1));

        Assert.Empty(cut.FindAll("[data-testid=presence-across]"));
        Assert.DoesNotContain("presence-can-cross", cut.Find("[data-testid=chart-presence]").ClassName);
        Assert.Equal(4, cut.FindAll("[data-testid=presence-row]").Count);
    }

    [Fact]
    public void TheChartPageRendersBothLayoutsAndLetsTheStylesheetChoose()
    {
        var cut = Render(Presence(1), true);

        Assert.Contains("presence-can-cross", cut.Find("[data-testid=chart-presence]").ClassName);
        Assert.Equal(4, cut.FindAll("[data-testid=presence-col]").Count);
        Assert.Equal(4, cut.FindAll("[data-testid=presence-row]").Count);
        // Gem names sit on their own row, a bracket under each gem with levels, the levels beneath.
        Assert.Equal(new[] { "OPAL", "DIAMOND" }, cut.FindAll(".presence-gem").Select(g => g.TextContent));
        Assert.Equal("grid-column:3 / span 3", cut.FindAll(".presence-bracket").Single().GetAttribute("style"));
        Assert.Equal(new[] { "LV.1", "LV.2", "LV.3" }, cut.FindAll(".presence-level").Select(l => l.TextContent));
    }

    [Fact]
    public void TitlesDownRunFiftyOnTheLeftToOneOnTheRight()
    {
        var cut = Render(Presence(1));

        var ticks = cut.FindAll(".presence-head .presence-row-plot b");
        Assert.Equal("#1", ticks.First().TextContent);
        Assert.Equal("left:100%", ticks.First().GetAttribute("style"));
        Assert.Equal("#50", ticks.Last().TextContent);
        Assert.Equal("left:0%", ticks.Last().GetAttribute("style"));
        // The ends always show; the tens give way to #25 alone on a plot too narrow for them.
        Assert.Equal(new[] { "#10", "#20", "#30", "#40" },
            ticks.Where(t => t.ClassName == "is-fine").Select(t => t.TextContent));
        Assert.Equal("#25", Assert.Single(ticks, t => t.ClassName == "is-coarse").TextContent);
        // A median of 12.5 sits 37.5 of 49 spots from #50.
        Assert.Equal($"left:{37.5 / 49 * 100:0.###}%",
            cut.FindAll("[data-testid=presence-row]")[1].QuerySelector(".presence-median")!.GetAttribute("style"));
    }

    [Fact]
    public void ATitlesTooltipSaysTheShareTheTypicalSpotsAndTheRating()
    {
        var cut = Render(Presence(1));

        Assert.Equal(new[]
        {
            "DIAMOND LV.1",
            "40% hold it",
            "Typically #8–#17 in top 50, centered around #13",
            "Rates higher than other D23s"
        }, Tip(cut.FindAll("[data-testid=presence-row]")[1]));
    }

    [Fact]
    public void AThinTitleSaysToReadItLooselyAndAnEmptyTitleSaysNobodyIsOnIt()
    {
        var cut = Render(Presence(1));
        var rows = cut.FindAll("[data-testid=presence-row]");

        Assert.Contains("is-thin", rows[2].ClassName);
        Assert.Equal(new[] { "90% hold it", "Typically #2 in top 50", "Low data count for this title, read loosely" },
            Lines(rows[2]));
        Assert.Equal("presence-tip-muted", rows[2].QuerySelectorAll(".presence-tip > *").Last().ClassName);
        Assert.Equal(new[] { "No players on this title yet" }, Lines(rows[3]));
        Assert.Equal("—", rows[3].QuerySelector(".presence-row-value")!.TextContent);
        Assert.Empty(rows[3].QuerySelectorAll(".presence-bar"));
    }

    [Fact]
    public void AFadedTitleNeverSetsTheShareScaleWhileASolidTitleHoldsTheChart()
    {
        // DIAMOND LV.2 is faded at 90%; the solid titles top out at 40%, so the panel stays at half scale
        // and the faded bar stops at its top.
        var cut = Render(Presence(1), true);

        Assert.Equal(new[] { "25%", "50%" }, ShareTicks(cut));
        Assert.Equal("height:100%", cut.FindAll("[data-testid=presence-col]")[2]
            .QuerySelector(".presence-bar")!.GetAttribute("style"));
    }

    [Fact]
    public void ASolidTitlePastFortyFivePercentOpensTheFullShareScale()
    {
        var presence = Presence(1);

        var cut = Render(presence with
        {
            Columns = presence.Columns.Select((c, i) => i == 1 ? c with { Holders = 20 } : c).ToArray()
        }, true);

        Assert.Equal(new[] { "50%", "100%" }, ShareTicks(cut));
    }

    [Fact]
    public void WhenOnlyFadedTitlesHoldTheChartTheirShareSetsTheScale()
    {
        var presence = Presence(2);

        var cut = Render(presence with
        {
            Columns = presence.Columns
                .Select(c => c.IsThin ? c : c with { Holders = 0, Spots = null, Dots = Array.Empty<double>() })
                .ToArray()
        }, true);

        Assert.Equal(new[] { "50%", "100%" }, ShareTicks(cut));
    }

    private static string[] ShareTicks(IRenderedComponent<ChartPumbilityPresence> cut)
    {
        return cut.FindAll(".presence-gutter span").Take(2).Select(s => s.TextContent).ToArray();
    }

    private IRenderedComponent<ChartPumbilityPresence> Render(ChartPumbilityPresenceRecord? presence,
        bool titlesAcross = false)
    {
        return RenderComponent<ChartPumbilityPresence>(p => p
            .Add(x => x.Presence, presence)
            .Add(x => x.Chart, D23)
            .Add(x => x.TitlesAcross, titlesAcross));
    }

    /// <summary>
    ///     Four titles: OPAL with three holders drawn as dots, DIAMOND LV.1 with a box, a thin DIAMOND LV.2
    ///     whose middle half rounds to one spot, and a DIAMOND LV.3 nobody is on.
    /// </summary>
    private static ChartPumbilityPresenceRecord Presence(int? mostHeld)
    {
        return new ChartPumbilityPresenceRecord(new[]
            {
                Column("[P.B] OPAL", null, 30, 3, null, new double[] { 4, 9, 20 }, null),
                Column("[P.B] DIAMOND", 1, 40, 16, new PumbilitySpots(3, 8, 12.5, 17, 30), Array.Empty<double>(),
                    PumbilityPresenceRating.Higher),
                Column("[P.B] DIAMOND", 2, 10, 9, new PumbilitySpots(1, 1.6, 2, 2.4, 6), Array.Empty<double>(), null),
                Column("[P.B] DIAMOND", 3, 0, 0, null, Array.Empty<double>(), null)
            }, mostHeld, Array.Empty<PumbilityPresenceRun>(), null,
            new DateTimeOffset(2026, 9, 16, 12, 30, 0, TimeSpan.Zero));
    }

    private static PumbilityPresenceColumn Column(string gem, int? level, int players, int holders,
        PumbilitySpots? spots, IReadOnlyList<double> dots, PumbilityPresenceRating? rating)
    {
        var band = level is { } l ? $"{gem} LV.{l}" : gem;
        return new PumbilityPresenceColumn(Name.From(band), Name.From(gem), level, players, holders, spots, dots,
            new PumbilitySpotBox(10, 15, 22), rating);
    }

    private static string[] Tip(AngleSharp.Dom.IElement row)
    {
        return row.QuerySelector(".presence-tip")!.Children.Select(c => c.TextContent).ToArray();
    }

    private static string[] Lines(AngleSharp.Dom.IElement row)
    {
        return row.QuerySelectorAll(".presence-tip > :not(.presence-tip-head)").Select(c => c.TextContent).ToArray();
    }
}
