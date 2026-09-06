using System;
using System.Threading;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Pages.Competition.MoM;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed partial class MoMSeasonPageTests
{
    [Fact]
    public void YourStandingIsYourBestSessionAndTheSegmentsCarryIt()
    {
        SignIn();
        Page(doublesStanding: new MoMStanding(3, 11, Guid.NewGuid(), 47300, 36,
            TimeSpan.FromMinutes(24) + TimeSpan.FromSeconds(46), 2));

        var cut = RenderComponent<Season>();

        var standing = cut.Find("[data-testid=mom-standing]");
        Assert.Contains("3rd", standing.TextContent);
        Assert.Contains("of 11", standing.TextContent);
        Assert.Contains("47,300", standing.TextContent);
        Assert.Contains("your best of 2 sessions", standing.TextContent);
        Assert.Contains("24:46 downtime", standing.TextContent);
        Assert.Contains("2 sessions", cut.FindAll("[data-testid=mom-board-segment]")[0].TextContent);
        Assert.NotEmpty(cut.FindAll(".mom-legend"));
    }

    [Fact]
    public void TheBoardYouPlayedOpensFirst()
    {
        // Singles only: the page opens on Singles (D33) — Doubles is the default only when both or neither.
        SignIn();
        Page(singlesStanding: new MoMStanding(2, 5, Guid.NewGuid(), 42596, 36, TimeSpan.FromMinutes(24), 1));

        var cut = RenderComponent<Season>();

        var segments = cut.FindAll("[data-testid=mom-board-segment]");
        Assert.Equal("false", segments[0].GetAttribute("aria-pressed"));
        Assert.Equal("true", segments[1].GetAttribute("aria-pressed"));
        Assert.Contains("Nobody has played Singles yet this season.", cut.Find(".mom-empty-board").TextContent);
        Assert.Contains("Singles", cut.Find("[data-testid=mom-standing] .k").TextContent);
    }

    [Fact]
    public void TheQueryStringPicksTheBoard()
    {
        Page();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/MarchOfMurlocs?board=Single");
        var cut = RenderComponent<Season>();
        Assert.Equal("true", cut.FindAll("[data-testid=mom-board-segment]")[1].GetAttribute("aria-pressed"));
        Assert.Empty(cut.FindAll("[data-testid=mom-board-row]"));
    }

    [Fact]
    public void PhoenixTwoExplainsItselfInsteadOfHidingTheSeason()
    {
        _mix = MixEnum.Phoenix2;
        Page();

        var cut = RenderComponent<Season>();

        Assert.Contains("Phoenix 2 boards open once the scoring is settled.", cut.Find("[data-testid=mom-standing]").TextContent);
        Assert.NotEmpty(cut.FindAll("[data-testid=mom-p2-notice]"));
        Assert.Empty(cut.FindAll("[data-testid=mom-board-segment]"));
        Assert.Empty(cut.FindAll("[data-testid=mom-board-row]"));
    }

    [Fact]
    public void NoSeasonYetIsOneCalmLine()
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetMoMSeasonPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoMSeasonPage?)null);
        var cut = RenderComponent<Season>();
        Assert.Contains("The first season is being set up.", cut.Markup);
    }
}
