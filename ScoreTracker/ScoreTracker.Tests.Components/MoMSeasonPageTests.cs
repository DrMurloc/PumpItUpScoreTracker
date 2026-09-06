using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.Models;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.Competition.MoM;
using ScoreTracker.Web.Services.Contracts;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Season page (docs/design/march-of-murlocs.md §11.2, D33): the live season leads with
///     your standing, one board shows at a time with the two segments as the switcher, rows wear
///     the board skin, and the foot walks to the neighbouring seasons. Static SSR throughout.
/// </summary>
public sealed partial class MoMSeasonPageTests : ComponentTestBase
{
    private static readonly DateTimeOffset Now = new(2025, 3, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Viewer = Guid.NewGuid();
    private static readonly Guid Doubles = Guid.NewGuid();
    private static readonly Guid Singles = Guid.NewGuid();
    private static readonly Guid Previous = Guid.NewGuid();
    private readonly Mock<IUiSettingsAccessor> _settings = new();
    private MixEnum _mix = MixEnum.Phoenix;

    public MoMSeasonPageTests()
    {
        _settings.Setup(s => s.GetSelectedMix(It.IsAny<CancellationToken>())).ReturnsAsync(() => _mix);
        Services.AddSingleton(_settings.Object);
        Services.AddSingleton(Mock.Of<IDateTimeOffsetAccessor>(d => d.Now == Now));
        Services.AddScoped<CommunityGlowReader>();
        Services.AddSingleton(Mock.Of<IUserRepository>());
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(false);
        SetRendererInfo(new RendererInfo("Static", false));
    }

    private static User Player(string name) =>
        new(Guid.NewGuid(), Name.From(name), true, null, new Uri("https://example.invalid/p.png"), null);

    private static MoMBoardRow Row(int place, User player, int total, int sessionNumber = 1) =>
        new(place, Guid.NewGuid(), player.Id, player, sessionNumber, total, 36, 23.9, TimeSpan.FromMinutes(25),
            new DateTimeOffset(2025, 2, 10 + place, 0, 0, 0, TimeSpan.Zero), null);

    private void SignIn()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(Player("DrMurloc") with { Id = Viewer });
        // The row tints read the viewer's communities and rivals; none here.
        Mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>());
        Mediator.Setup(m => m.Send(It.IsAny<GetMyRivalsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RivalSubject>());
    }

    private void Page(MoMStanding? doublesStanding = null, MoMStanding? singlesStanding = null,
        bool viewerHasPublished = false)
    {
        var kim = Player("김재현");
        var rows = new[] { Row(1, kim, 59319), Row(2, Player("yimmythe42"), 57325), Row(3, kim, 41780, 2) };
        var season = new MoMSeasonSummary(Guid.NewGuid(), "Winter 2025",
            new DateTimeOffset(2025, 2, 2, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 3, 31, 23, 59, 59, TimeSpan.Zero), true);
        var page = new MoMSeasonPage(season, new[]
            {
                new MoMBoardView(Doubles, ChartType.Double, MixEnum.Phoenix, TimeSpan.FromMinutes(105), rows, doublesStanding),
                new MoMBoardView(Singles, ChartType.Single, MixEnum.Phoenix, TimeSpan.FromMinutes(105), Array.Empty<MoMBoardRow>(), singlesStanding)
            },
            new MoMSeasonSummary(Previous, "March of Murlocs 2", season.StartsAt.AddMonths(-8), season.StartsAt.AddMonths(-6), false),
            null, viewerHasPublished);
        Mediator.Setup(m => m.Send(It.IsAny<GetMoMSeasonPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetMoMSeasonPageQuery q, CancellationToken _) => q.Mix == MixEnum.Phoenix2
                ? page with { Boards = page.Boards.Select(b => b with { Mix = MixEnum.Phoenix2 }).ToArray() }
                : page);
    }

    [Fact]
    public void TheLiveSeasonLeadsWithTheDoublesBoardRankedInScoreOrder()
    {
        Page();

        var cut = RenderComponent<Season>();

        Assert.Contains("Winter 2025", cut.Find("h1.mom-title").TextContent);
        Assert.Contains("28 days left", cut.Markup); // 27.5 days to the end, rounded up
        var rows = cut.FindAll("[data-testid=mom-board-row]");
        Assert.Equal(3, rows.Count);
        Assert.Contains("59,319", rows[0].TextContent);
        Assert.Contains("2nd session", rows[2].TextContent);
        // No standing, no standing panel: an empty one repeated the board and carried a second
        // copy of Record a session (owner, 2026-09-06).
        Assert.Empty(cut.FindAll("[data-testid=mom-standing]"));
        var segments = cut.FindAll("[data-testid=mom-board-segment]");
        Assert.Equal("true", segments[0].GetAttribute("aria-pressed"));
        Assert.Equal("/MarchOfMurlocs?board=Single", segments[1].GetAttribute("href"));
        Assert.Equal($"/MarchOfMurlocs/{Previous}", cut.Find("[data-testid=mom-previous-season]").GetAttribute("href"));
        Assert.Contains("This is the current season", cut.Find(".mom-foot").TextContent);
        Assert.Empty(cut.FindAll(".mom-legend")); // a visitor has no relationships to explain
        // The foot's collapsed summary is gone (D44); a visitor gets the newcomer card instead.
        Assert.Empty(cut.FindAll("details.mom-rules"));
        Assert.NotEmpty(cut.FindAll("[data-testid=mom-howto]"));
    }
}
