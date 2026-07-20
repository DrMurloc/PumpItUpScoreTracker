using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using Bunit.TestDoubles;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Pages.Communities;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The restyled community Rankings tab: PUMBILITY is the primary board with Singles /
///     Doubles / competitive level riding along as columns; the Total board swaps those for
///     Highest Level / Clear Count.
/// </summary>
public sealed class CommunityLeaderboardPageTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUiSettingsAccessor> _uiSettings = new();

    private static readonly Guid PlayerId = Guid.NewGuid();

    public CommunityLeaderboardPageTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(PlayerId, Name.From("Tester"), true, null,
            new Uri("https://piu.test/avatar.png"), null));
        _uiSettings.Setup(u => u.GetSelectedMix(It.IsAny<CancellationToken>())).ReturnsAsync(MixEnum.Phoenix);

        var community = new Community(Name.From("Acme"), Guid.NewGuid(), CommunityPrivacyType.Public,
            new[] { PlayerId }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), false);
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(community);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CommunityOverviewRecord(Name.From("Acme"), CommunityPrivacyType.Public, 1, false) });
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityLeaderboardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CommunityLeaderboardRecord(Name.From("Tester"), true,
                    new Uri("https://piu.test/avatar.png"), PlayerId,
                    12000, 22, 500, 300, 900000, 867, 950000, 20.5, 871, 960000, 20.9, 852, 940000, 20.1,
                    20.6, 20.8, 20.2)
            });
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunityRoleQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Communities.Contracts.CommunityRoleRecord(CommunityRole.Member, CommunityPermission.None));
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityPlayCountsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [PlayerId] = 42 });
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityCoOpCompletionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double> { [PlayerId] = 0.5 });
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityRosterQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Communities.Contracts.CommunityMemberRecord(PlayerId, Name.From("Tester"),
                    new Uri("https://piu.test/avatar.png"), CommunityRole.Member, CommunityPermission.None, true),
                new Communities.Contracts.CommunityMemberRecord(Guid.NewGuid(), Name.From("Newcomer"),
                    new Uri("https://piu.test/avatar.png"), CommunityRole.Member, CommunityPermission.None, true),
                new Communities.Contracts.CommunityMemberRecord(Guid.NewGuid(), Name.From("Exiled"),
                    new Uri("https://piu.test/avatar.png"), CommunityRole.Banned, CommunityPermission.None, true)
            });
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new User(PlayerId, Name.From("Tester"), true, null, new Uri("https://piu.test/avatar.png"), null)
            });

        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(_users.Object);
        Services.AddSingleton(_uiSettings.Object);
    }

    private IRenderedComponent<CommunityLeaderboard> Render()
    {
        // UserLabel gates its tooltip on RendererInfo — declare the interactive world.
        this.RenderInteractive();
        Services.GetRequiredService<FakeNavigationManager>()
            .NavigateTo("/Community/Leaderboard?CommunityName=Acme");
        return RenderComponent<CommunityLeaderboard>();
    }

    [Fact]
    public void CombinedBoardShowsPumbilityCompetitiveAndPlayCountOnCompactRows()
    {
        // The board wears the rankings row look — a labelled value per figure, no table header.
        var cut = Render();
        Assert.NotEmpty(cut.FindAll(".olb-rank-card"));
        Assert.Empty(cut.FindAll("th"));
        Assert.Contains("title=\"PUMBILITY\"", cut.Markup);
        Assert.Contains("title=\"Comp Lv\"", cut.Markup);
        Assert.Contains("title=\"Charts Played\"", cut.Markup);
        // The board types ride the tier-list-style toggle, not table columns.
        var toggles = cut.FindAll("button").Select(b => b.TextContent.Trim()).ToArray();
        Assert.Contains("Combined", toggles);
        Assert.Contains("Singles", toggles);
        Assert.Contains("Doubles", toggles);
        Assert.Contains("CoOp", toggles);
    }

    [Fact]
    public void RankingsRenderThePlayerRowWithPlayCount()
    {
        var cut = Render();
        Assert.Contains("Tester", cut.Markup);
        Assert.Contains("867", cut.Markup);
        Assert.Contains("42", cut.Markup);
    }

    [Fact]
    public void CoOpBoardSwapsCompetitiveForCompletion()
    {
        var cut = Render();
        cut.FindAll("button").First(b => b.TextContent.Trim() == "CoOp").Click();
        Assert.Contains("title=\"CoOp Completion\"", cut.Markup);
        Assert.DoesNotContain("title=\"Comp Lv\"", cut.Markup);
        Assert.Contains("50", cut.Markup);
    }

    [Fact]
    public void RecapFlameOnlyRendersOnPhoenix()
    {
        var cut = Render();
        Assert.Contains("/PhoenixRecap", cut.Markup);

        _uiSettings.Setup(u => u.GetSelectedMix(It.IsAny<CancellationToken>())).ReturnsAsync(MixEnum.Phoenix2);
        var phoenix2 = Render();
        Assert.DoesNotContain("/PhoenixRecap", phoenix2.Markup);
    }

    [Fact]
    public void ByChartAndMembersTabsAreGone()
    {
        var cut = Render();
        Assert.DoesNotContain("By Chart", cut.Markup);
        Assert.DoesNotContain("Shared PGs", cut.Markup);
    }

    [Fact]
    public void PlaystyleBandsTheStoredTopFiftyAverageAndWearsItsGradeColor()
    {
        // SkillScore 950,000 sits on the AAA floor — the Pass Refiner band — and the chip
        // carries that band's grade color, computed from site stats (no official-mirror data).
        var cut = Render();
        Assert.Contains("Pass Refiner", cut.Markup);
        Assert.Contains(PlayerTypeChip.ChipStyle(RecapPlayerType.PassRefiner), cut.Markup);
    }

    [Fact]
    public void MembersWithoutScoresRollCallBelowTheTableWithoutBans()
    {
        var cut = Render();
        Assert.Contains("No scores in this mix yet", cut.Markup);
        Assert.Contains("Newcomer", cut.Markup);
        Assert.DoesNotContain("Exiled", cut.Markup);
        // The roster (bans excluded) is the member count, not just the scored rows.
        Assert.Contains("2 members", cut.Markup);
    }
}
