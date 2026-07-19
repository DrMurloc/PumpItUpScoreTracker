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
        Assert.Contains("Open world board", cut.Markup);
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
    public void ManageShowsOnlyForCreatorOrAdmin()
    {
        GivenLoggedIn();
        GivenPublicCommunities(Overview("Led"), Overview("Joined"));
        GivenMyCommunities(Overview("Led"), Overview("Joined"));
        GivenRoles(
            new MyCommunityRoleRecord(Name.From("Led"), CommunityRole.Creator, CommunityPermission.All),
            new MyCommunityRoleRecord(Name.From("Joined"), CommunityRole.Member, CommunityPermission.None));
        var cut = Render();

        var cards = cut.FindAll(".communities-your .communities-card");
        var ledCard = cards.First(c => c.TextContent.Contains("Led"));
        var joinedCard = cards.First(c => c.TextContent.Contains("Joined"));
        Assert.Contains("Manage", ledCard.TextContent);
        Assert.DoesNotContain("Manage", joinedCard.TextContent);
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

    [Fact]
    public void InviteShowsOnlyWithTheInvitePermissionOnCodeCommunities()
    {
        GivenLoggedIn();
        GivenPublicCommunities(
            Overview("CodeLed", privacy: CommunityPrivacyType.PublicWithCode),
            Overview("CodeJoined", privacy: CommunityPrivacyType.PublicWithCode));
        GivenMyCommunities(
            Overview("CodeLed", privacy: CommunityPrivacyType.PublicWithCode),
            Overview("CodeJoined", privacy: CommunityPrivacyType.PublicWithCode));
        GivenRoles(
            new MyCommunityRoleRecord(Name.From("CodeLed"), CommunityRole.Admin,
                CommunityPermission.ManageInviteLinks),
            new MyCommunityRoleRecord(Name.From("CodeJoined"), CommunityRole.Member, CommunityPermission.None));
        var cut = Render();

        var cards = cut.FindAll(".communities-your .communities-card");
        // The privacy chip says "Invite Required" on both cards, so assert on the buttons.
        Assert.Contains(cards.First(c => c.TextContent.Contains("CodeLed")).QuerySelectorAll("button"),
            b => b.TextContent.Trim() == "Invite");
        Assert.DoesNotContain(cards.First(c => c.TextContent.Contains("CodeJoined")).QuerySelectorAll("button"),
            b => b.TextContent.Trim() == "Invite");
    }
}
