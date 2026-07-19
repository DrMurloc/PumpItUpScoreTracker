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
        _mediator.Setup(m => m.Send(It.IsAny<OfficialMirror.Contracts.Queries.GetOfficialPlayerTypesQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, RecapPlayerType> { [PlayerId] = RecapPlayerType.Competitive });
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
    public void CombinedBoardShowsPumbilityCompetitiveAndPlayCountColumns()
    {
        var cut = Render();
        var headers = cut.FindAll("th").Select(h => h.TextContent.Trim()).ToArray();
        Assert.Contains("PUMBILITY", headers);
        Assert.Contains("Comp Lv", headers);
        Assert.Contains("Charts Played", headers);
        Assert.DoesNotContain("Highest Level", headers);
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
        var headers = cut.FindAll("th").Select(h => h.TextContent.Trim()).ToArray();
        Assert.Contains("CoOp Completion", headers);
        Assert.DoesNotContain("Comp Lv", headers);
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
}
