using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services.Contracts;
using Xunit;
using CommunitiesPage = ScoreTracker.Web.Pages.Communities.Communities;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The directory splits regional communities (World card + Regions rail) from player
///     communities (Your Communities + Explore), and gates Manage/Invite on the caller's role.
/// </summary>
public sealed class CommunitiesDirectoryPageTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUiSettingsAccessor> _uiSettings = new();

    public CommunitiesDirectoryPageTests()
    {
        _users.Setup(u => u.GetCountries(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CountryRecord("Spain", new Uri("https://piu.test/spain.png")) });
        _uiSettings.Setup(u => u.GetSelectedMix(It.IsAny<CancellationToken>())).ReturnsAsync(MixEnum.Phoenix);
        _mediator.Setup(m => m.Send(It.IsAny<GetPlayerStatsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(Guid.NewGuid(), 0, 1, 0, 0, 0, 867, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 0));
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunityRolesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MyCommunityRoleRecord>());
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>());
        _mediator.Setup(m => m.Send(It.IsAny<GetPublicCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>());
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityCompetitiveRangesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityCompetitiveRangeRecord>());
        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(_users.Object);
        Services.AddSingleton(_uiSettings.Object);
    }

    private void GivenLoggedIn(string? country = "Spain")
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(Guid.NewGuid(), Name.From("Tester"), true, null,
            new Uri("https://piu.test/avatar.png"), country == null ? null : Name.From(country)));
    }

    private void GivenPublicCommunities(params CommunityOverviewRecord[] overviews) =>
        _mediator.Setup(m => m.Send(It.IsAny<GetPublicCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(overviews);

    private void GivenMyCommunities(params CommunityOverviewRecord[] overviews) =>
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(overviews);

    private void GivenRoles(params MyCommunityRoleRecord[] roles) =>
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunityRolesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles);

    private static CommunityOverviewRecord Overview(string name, bool regional = false, int members = 5,
        CommunityPrivacyType privacy = CommunityPrivacyType.Public) =>
        new(Name.From(name), privacy, members, regional);

    private IRenderedComponent<CommunitiesPage> Render() => RenderComponent<CommunitiesPage>();

    [Fact]
    public void RegionalCommunitiesLandInTheRailNotThePlayerLists()
    {
        GivenLoggedIn();
        GivenPublicCommunities(Overview("World", regional: true, members: 18392),
            Overview("Spain", regional: true, members: 610),
            Overview("Korea", regional: true, members: 4120),
            Overview("Storm Crew"));
        var cut = Render();

        // World renders as the rail card, Korea in the browse list, Spain as "your region";
        // none of them get Join/Leave player-card affordances.
        Assert.Contains("Everyone who has gone public", cut.Markup);
        Assert.Contains("Korea", cut.Markup);
        var yourSection = cut.Find(".communities-your");
        Assert.DoesNotContain("World", yourSection.TextContent);
        var exploreSection = cut.Find(".communities-explore");
        Assert.DoesNotContain("Korea", exploreSection.TextContent);
        Assert.Contains("Storm Crew", exploreSection.TextContent);
    }

    [Fact]
    public void YourCommunitiesSplitFromExploreByMembership()
    {
        GivenLoggedIn();
        GivenPublicCommunities(Overview("Explorable"), Overview("Mine"));
        GivenMyCommunities(Overview("Mine"));
        GivenRoles(new MyCommunityRoleRecord(Name.From("Mine"), CommunityRole.Member, CommunityPermission.None));
        var cut = Render();

        Assert.Contains("Mine", cut.Find(".communities-your").TextContent);
        Assert.Contains("Explorable", cut.Find(".communities-explore").TextContent);
        Assert.DoesNotContain("Explorable", cut.Find(".communities-your").TextContent);
    }

    [Fact]
    public void ManageAndInviteLiveOnTheCommunityPageNotTheDirectory()
    {
        GivenLoggedIn();
        GivenPublicCommunities(Overview("Led", privacy: CommunityPrivacyType.PublicWithCode));
        GivenMyCommunities(Overview("Led", privacy: CommunityPrivacyType.PublicWithCode));
        GivenRoles(new MyCommunityRoleRecord(Name.From("Led"), CommunityRole.Creator, CommunityPermission.All));
        var cut = Render();

        var buttons = cut.FindAll(".communities-card button").Select(b => b.TextContent.Trim()).ToArray();
        Assert.DoesNotContain("Manage", buttons);
        Assert.DoesNotContain("Invite", buttons);
        Assert.Contains("Rankings", buttons);
    }

    [Fact]
    public void LegacyWorldWithoutTheRegionalFlagStillLandsInTheRail()
    {
        // System communities created before the IsRegional migration carry a false flag;
        // the directory classifies World (and country names) by name as the fallback.
        GivenLoggedIn();
        GivenPublicCommunities(Overview("World", regional: false, members: 18392),
            Overview("Spain", regional: false, members: 610),
            Overview("Storm Crew"));
        var cut = Render();

        Assert.DoesNotContain("World", cut.Find(".communities-your").TextContent);
        Assert.DoesNotContain("World", cut.Find(".communities-explore").TextContent);
        Assert.Contains("Storm Crew", cut.Find(".communities-explore").TextContent);
        Assert.Contains("18392", cut.Find(".communities-world").TextContent.Replace(",", "").Replace(".", ""));
    }

    [Fact]
    public void CompetitiveRangesRenderOnCards()
    {
        GivenLoggedIn();
        GivenPublicCommunities(Overview("Storm Crew"));
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityCompetitiveRangesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CommunityCompetitiveRangeRecord(Name.From("Storm Crew"), 5.2, 22.4, 9.1, 18.4)
            });
        var cut = Render();

        var card = cut.Find(".communities-explore").TextContent;
        Assert.Contains("Singles 5.2-22.4", card);
        Assert.Contains("Doubles 9.1-18.4", card);
    }

    [Fact]
    public void CreatorGetsNoLeaveButtonButMembersDo()
    {
        GivenLoggedIn();
        GivenPublicCommunities(Overview("Led"), Overview("Joined"));
        GivenMyCommunities(Overview("Led"), Overview("Joined"));
        GivenRoles(
            new MyCommunityRoleRecord(Name.From("Led"), CommunityRole.Creator, CommunityPermission.All),
            new MyCommunityRoleRecord(Name.From("Joined"), CommunityRole.Member, CommunityPermission.None));
        var cut = Render();

        var cards = cut.FindAll(".communities-your .communities-card");
        Assert.DoesNotContain("Leave", cards.First(c => c.TextContent.Contains("Led")).TextContent);
        Assert.Contains("Leave", cards.First(c => c.TextContent.Contains("Joined")).TextContent);
    }

}
