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
using ScoreTracker.Web.Services;
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
    public void PhoenixTwoShowsItsOwnBoards()
    {
        // D38: the Phoenix 2 boards are live, and the page treats them exactly as Phoenix's.
        _mix = MixEnum.Phoenix2;
        Page();

        var cut = RenderComponent<Season>();

        Assert.Contains("Phoenix 2 · March of Murlocs", cut.Find(".pmb-eyebrow").TextContent);
        Assert.Equal(2, cut.FindAll("[data-testid=mom-board-segment]").Count);
        Assert.NotEmpty(cut.FindAll("[data-testid=mom-board-row]"));
        Assert.Empty(cut.FindAll("[data-testid=mom-no-boards]"));
        Assert.DoesNotContain("scoring is settled", cut.Markup);
    }

    [Fact]
    public void ASeasonWithoutThisMixBoardsYetSaysSoInOneLine()
    {
        // Between a season's creation and the daily heal (D43) a mix may have no boards.
        _mix = MixEnum.Phoenix2;
        Page();
        Mediator.Setup(m => m.Send(It.IsAny<GetMoMSeasonPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetMoMSeasonPageQuery q, CancellationToken _) =>
                new MoMSeasonPage(new MoMSeasonSummary(Guid.NewGuid(), "Summer 2026", Now.AddDays(-20), Now.AddDays(40), true),
                    Array.Empty<MoMBoardView>(), null, null));

        var cut = RenderComponent<Season>();

        Assert.NotEmpty(cut.FindAll("[data-testid=mom-no-boards]"));
        Assert.Empty(cut.FindAll("[data-testid=mom-board-segment]"));
        Assert.Empty(cut.FindAll("[data-testid=mom-standing]"));
    }

    [Fact]
    public void BothRulesLinksLandOnTheRulesPage()
    {
        Page();

        var cut = RenderComponent<Season>();

        Assert.Equal(MoMText.RulesRoute, cut.Find(".pmb-eyebrow-link").GetAttribute("href"));
        Assert.Equal(MoMText.RulesRoute, cut.Find(".mom-rules a").GetAttribute("href"));
        Assert.DoesNotContain("docs.google.com", cut.Markup);
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
