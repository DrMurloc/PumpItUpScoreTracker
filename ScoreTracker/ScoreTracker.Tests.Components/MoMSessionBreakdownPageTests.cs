using System;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.Web.Pages.Competition.MoM;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Session Breakdown page (§11.3, D26): hero, then where the points came from, then the
///     session in a density with its pace, then Compare — in that order — and a jacket that opens
///     the chart dialog without playing anything (D31).
/// </summary>
public sealed class MoMSessionBreakdownPageTests : ComponentTestBase
{
    private readonly MoMSessionView _view = MoMComponentData.Session();

    public MoMSessionBreakdownPageTests()
    {
        Services.AddSingleton(Mock.Of<IUserRepository>());
        Services.AddSingleton(Mock.Of<IDateTimeOffsetAccessor>(d => d.Now == new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero)));
        Mediator.Setup(m => m.Send(It.IsAny<GetMoMSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetMoMSessionQuery q, CancellationToken _) => q.SessionId == _view.SessionId ? _view : null);
        Mediator.Setup(m => m.Send(It.IsAny<CompareMoMSessionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoMComparison?)null);
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(false);
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private IRenderedComponent<SessionBreakdown> Render(Guid? id = null) =>
        RenderComponent<SessionBreakdown>(p => p.Add(s => s.SessionId, id ?? _view.SessionId));

    [Fact]
    public void TheHeroThenTheNumbersThenTheSessionThenCompare()
    {
        var cut = Render();

        Assert.Equal("1st", cut.Find("[data-testid=mom-hero-place]").TextContent);
        Assert.Equal("6,388", cut.Find("[data-testid=mom-hero-total]").TextContent);
        Assert.Contains("Points · 1st of 3", cut.Find(".sbd-tally").TextContent);
        Assert.Contains("김재현", cut.Find(".sbd-hero-title").TextContent);
        Assert.Contains("Doubles · Phoenix", cut.Find(".sbd-hero-tag").TextContent);
        Assert.Contains("3 charts", cut.Find(".mom-formula").TextContent);
        Assert.Contains("2,129", cut.Find(".mom-formula").TextContent); // per chart
        var markup = cut.Markup;
        var numbers = markup.IndexOf("mom-four", StringComparison.Ordinal);
        var cards = markup.IndexOf("mom-chart-cards", StringComparison.Ordinal);
        var pace = markup.IndexOf("mom-pace", StringComparison.Ordinal);
        var compare = markup.IndexOf("mom-compare", StringComparison.Ordinal);
        Assert.True(numbers > 0 && numbers < cards && cards < pace && pace < compare, "sections out of order");
        Assert.Equal(3, cut.FindAll("[data-testid=mom-chart-card]").Count);
        Assert.Contains("rest is derived for hand-entered sessions", markup);
        Assert.Contains("href=\"/MarchOfMurlocs\"", markup);
        Assert.Contains("?board=Double", cut.Find(".pmb-eyebrow-link").GetAttribute("href"));
        Assert.Contains("Watch", markup);
        Assert.DoesNotContain("AutoPlay=\"True\"", markup);
    }

    [Fact]
    public async Task TheDensityButtonsSwapTheListInPlace()
    {
        var cut = Render();

        await cut.Find("button[aria-label=Compact]").ClickAsync(new MouseEventArgs());
        Assert.Equal(3, cut.FindAll("[data-testid=mom-chart-sticker]").Count);
        Assert.Empty(cut.FindAll("[data-testid=mom-chart-card]"));

        await cut.Find("button[aria-label=Table]").ClickAsync(new MouseEventArgs());
        Assert.Equal(3, cut.FindAll("[data-testid=mom-chart-row]").Count);
    }

    [Fact]
    public void AnUnknownSessionIsOneCalmLineWithTheWayBack()
    {
        var cut = Render(Guid.NewGuid());
        Assert.Contains("This session isn't here.", cut.Markup);
        Assert.Contains("Back to the season", cut.Markup);
    }

    [Fact]
    public void ADraftWearsAChipInsteadOfAPlace()
    {
        var draft = MoMComponentData.Session(draft: true);
        Mediator.Setup(m => m.Send(It.IsAny<GetMoMSessionQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(draft);

        var cut = Render(draft.SessionId);

        Assert.Empty(cut.FindAll("[data-testid=mom-hero-place]"));
        Assert.Contains("Draft", cut.Find(".mom-chip-draft").TextContent);
        Assert.Equal("Points", cut.Find(".sbd-tally-k").TextContent.Trim());
    }
}
