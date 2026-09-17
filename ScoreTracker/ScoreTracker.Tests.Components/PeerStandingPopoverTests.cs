using System;
using System.Collections.Generic;
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

public sealed class PeerStandingPopoverTests : ComponentTestBase
{
    private static readonly Guid Club = Guid.NewGuid();
    private static readonly DateTimeOffset Sealed = new(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);

    private static Chart TestChart() => new(Guid.NewGuid(), MixEnum.Phoenix,
        new Song(Name.From("Cleaner"), SongType.Arcade, new Uri("https://piuimages.arroweclip.se/probe.png"),
            TimeSpan.Zero, Name.From("Probe"), null),
        ChartType.Single, DifficultyLevel.From(20), MixEnum.Phoenix, null, null);

    private static PeerStanding Full() => new(94, 71, 5, 0, 5, new[]
    {
        new PeerStandingSource(PeerSourceKind.Rivals, null, null, false, false, 6, 5, 1, 2),
        new PeerStandingSource(PeerSourceKind.Community, Club, "NorCal Pump", false, false, 30, 12, 2, 0),
        new PeerStandingSource(PeerSourceKind.Community, Guid.NewGuid(), "United States", true, false, 110, 60, 5, 0),
        new PeerStandingSource(PeerSourceKind.CompetitiveLevel, null, null, false, false, 88, 60, 5, 0)
    }, Sealed);

    private IRenderedComponent<PeerStandingPopover> Render(PeerStanding? standing, bool sourcesChosen = true,
        EventCallback<PeerBoardRequest> onOpen = default)
    {
        return RenderComponent<PeerStandingPopover>(p => p
            .Add(x => x.Standing, standing)
            .Add(x => x.Chart, TestChart())
            .Add(x => x.SourcesChosen, sourcesChosen)
            .Add(x => x.OnOpenBoard, onOpen));
    }

    private static GradeProgress Progress(int score) => GradeProgress.Of(PhoenixScore.From(score), MixEnum.Phoenix2);

    private IRenderedComponent<PeerStandingPopover> RenderUnderAGradeRule(PeerStanding? standing, int score,
        bool sourcesChosen = true, bool showNextGrade = true)
    {
        return RenderComponent<PeerStandingPopover>(p => p
            .Add(x => x.Standing, standing)
            .Add(x => x.Chart, TestChart())
            .Add(x => x.SourcesChosen, sourcesChosen)
            .Add(x => x.Progress, Progress(score))
            .Add(x => x.ShowNextGrade, showNextGrade));
    }

    [Fact]
    public void UnderAGradeRuleTheLineSaysHowFarToTheNextGrade()
    {
        var cut = RenderUnderAGradeRule(Full(), 989_050);

        var line = cut.Find("[data-testid='peer-pop-next']");
        Assert.Contains("<b>950</b> to SSS", line.InnerHtml);
        Assert.Contains("81% of the way through SS+", line.TextContent);
    }

    [Fact]
    public void SssPlusCountsToAPerfectGame()
    {
        var cut = RenderUnderAGradeRule(Full(), 999_420);

        Assert.Contains("<b>580</b> to a Perfect Game", cut.Find("[data-testid='peer-pop-next']").InnerHtml);
    }

    [Fact]
    public void TheLineShowsInEveryStateBecauseTheGlowNeedsNoPeers()
    {
        var noPeerPassed = PeerStanding.NoCohort(12, 3, Array.Empty<PeerStandingSource>());

        Assert.Single(RenderUnderAGradeRule(noPeerPassed, 989_050).FindAll("[data-testid='peer-pop-next']"));
        Assert.Single(RenderUnderAGradeRule(null, 989_050).FindAll("[data-testid='peer-pop-next']"));
        Assert.Single(RenderUnderAGradeRule(null, 989_050, sourcesChosen: false).FindAll("[data-testid='peer-pop-next']"));
    }

    [Fact]
    public void APeerRuleOrAPerfectGamePrintsNoLine()
    {
        Assert.Empty(RenderUnderAGradeRule(Full(), 989_050, showNextGrade: false).FindAll("[data-testid='peer-pop-next']"));
        Assert.Empty(RenderUnderAGradeRule(Full(), 1_000_000).FindAll("[data-testid='peer-pop-next']"));
    }

    [Fact]
    public void LeadsWithThePlaceThenWhoHasNotPassedItThenOneLinePerSource()
    {
        var cut = Render(Full());

        Assert.Contains("#6 of 72 peers", cut.Find("[data-testid='peer-pop-head']").TextContent);
        // 94 peers, 71 passed: 23 more have not, 5 of them broke it.
        Assert.Contains("23 more haven't passed it (5 broke)", cut.Markup);
        Assert.Equal(4, cut.FindAll("[data-testid='peer-pop-source']").Count);
        Assert.Contains("NorCal Pump", cut.Markup);
        Assert.Contains("United States · Region", cut.Markup);
        Assert.Contains("#3 of 13", cut.Markup);
    }

    [Fact]
    public void ABoardOnlyRivalIsFootnotedWithTheMirrorsDate()
    {
        var cut = Render(Full());

        Assert.Contains("2 from the official board*", cut.Markup);
        Assert.Contains("* Official board data, as of 31 Aug.", cut.Markup);
    }

    [Fact]
    public async Task ASourceLineAsksTheHostForThatSourcesOwnBoard()
    {
        PeerBoardRequest? asked = null;
        var cut = Render(Full(), onOpen: EventCallback.Factory.Create<PeerBoardRequest>(this, r => asked = r));

        await cut.FindAll("[data-testid='peer-pop-source']")[1].ClickAsync(new MouseEventArgs());

        Assert.NotNull(asked);
        Assert.Equal(ChartLeaderboardScopes.LeaderboardScope.Community, asked!.Scope);
        Assert.Equal("NorCal Pump", asked.Community?.ToString());
    }

    [Fact]
    public void WithoutAHostToOpenBoardsTheLinesArePlainText()
    {
        var cut = Render(Full());

        Assert.Empty(cut.FindAll("a[data-testid='peer-pop-source']"));
    }

    [Fact]
    public void NoPeerPassedItSaysSoAndCountsWhoBroke()
    {
        var none = PeerStanding.NoCohort(12, 3, new[]
        {
            new PeerStandingSource(PeerSourceKind.Rivals, null, null, false, false, 12, 0, 0, 0)
        });

        var cut = Render(none);

        Assert.Contains("None of your 12 peers have passed this yet.", cut.Markup);
        Assert.Contains("3 of them tried and broke.", cut.Markup);
    }

    [Fact]
    public void NothingTickedPointsAtTheAccountPage()
    {
        var cut = Render(null, sourcesChosen: false);

        Assert.Contains("You have no peer groups selected.", cut.Markup);
        Assert.Contains("href=\"/Account?tab=peers\"", cut.Markup);
    }

    /// <summary>
    ///     Nothing has measured the score yet — the peers read is in flight, or the load that would
    ///     have carried it was superseded. The popover must not answer a question nobody asked:
    ///     the reporter tapped a Perfect Game and was told none of his peers had passed it
    ///     (2026-09-06).
    /// </summary>
    [Fact]
    public void AnUnmeasuredScoreSaysSoRatherThanClaimingNobodyPassedIt()
    {
        var cut = RenderComponent<PeerStandingPopover>(p => p
            .Add(x => x.Standing, null)
            .Add(x => x.SourcesChosen, true));

        Assert.Contains("Still working out where this sits.", cut.Markup);
        Assert.DoesNotContain("have passed this yet", cut.Markup);
    }
}
