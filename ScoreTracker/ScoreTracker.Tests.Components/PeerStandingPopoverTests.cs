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
        Assert.DoesNotContain("Each line opens that board.", cut.Markup);
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
}
