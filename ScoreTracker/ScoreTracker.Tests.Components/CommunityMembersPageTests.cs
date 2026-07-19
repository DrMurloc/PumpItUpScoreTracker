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
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.Communities;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Members tab renders the roster for everyone but gates management controls on the
///     caller's role/permissions (the aggregate still authorizes server-side).
/// </summary>
public sealed class CommunityMembersPageTests : ComponentTestBase
{
    private static readonly Guid CreatorId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid BannedId = Guid.NewGuid();

    private readonly Mock<IMediator> _mediator = new();

    public CommunityMembersPageTests()
    {
        var community = new Community(Name.From("Acme"), CreatorId, CommunityPrivacyType.Private,
            new[]
            {
                new CommunityMember(CreatorId, CommunityRole.Creator, CommunityPermission.All, null, null),
                new CommunityMember(AdminId, CommunityRole.Admin, CommunityPermission.ManageInviteLinks, CreatorId,
                    null),
                new CommunityMember(MemberId, CommunityRole.Member, CommunityPermission.None, null, null),
                new CommunityMember(BannedId, CommunityRole.Banned, CommunityPermission.None, null, null)
            },
            Array.Empty<Community.ChannelConfiguration>(), new Dictionary<Guid, DateOnly?>(), false,
            Community.DefaultAdminPermissionsSeed, null);

        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(community);
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityRosterQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Row(CreatorId, "TheCreator", CommunityRole.Creator, CommunityPermission.All),
                Row(AdminId, "TheAdmin", CommunityRole.Admin, CommunityPermission.ManageInviteLinks),
                Row(MemberId, "TheMember", CommunityRole.Member, CommunityPermission.None),
                Row(BannedId, "TheBanned", CommunityRole.Banned, CommunityPermission.None)
            });
        Services.AddSingleton(_mediator.Object);
    }

    private static CommunityMemberRecord Row(Guid id, string name, CommunityRole role,
        CommunityPermission permissions) =>
        new(id, Name.From(name), new Uri("https://piu.test/avatar.png"), role, permissions);

    private void GivenMyRole(CommunityRole? role, CommunityPermission permissions)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunityRoleQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommunityRoleRecord(role, permissions));
    }

    private IRenderedComponent<CommunityMembers> Render()
    {
        // CommunityName is [SupplyParameterFromQuery] — it binds from the URI, not from
        // component parameters.
        Services.GetRequiredService<Bunit.TestDoubles.FakeNavigationManager>()
            .NavigateTo("/Community/Members?CommunityName=Acme");
        return RenderComponent<CommunityMembers>();
    }

    [Fact]
    public void RosterRendersEveryMemberIncludingBans()
    {
        GivenMyRole(CommunityRole.Member, CommunityPermission.None);
        var cut = Render();
        Assert.Contains("TheCreator", cut.Markup);
        Assert.Contains("TheAdmin", cut.Markup);
        Assert.Contains("TheMember", cut.Markup);
        Assert.Contains("TheBanned", cut.Markup);
    }

    [Fact]
    public void PlainMemberSeesNoManagementControls()
    {
        GivenMyRole(CommunityRole.Member, CommunityPermission.None);
        var cut = Render();
        Assert.DoesNotContain("Promote to Admin", cut.Markup);
        Assert.DoesNotContain("Ban", cut.FindAll("button").Select(b => b.TextContent.Trim()));
        Assert.DoesNotContain("Delete Community", cut.Markup);
    }

    [Fact]
    public void CreatorSeesFullManagementSurface()
    {
        GivenMyRole(CommunityRole.Creator, CommunityPermission.All);
        var cut = Render();
        Assert.Contains("Promote to Admin", cut.Markup);
        Assert.Contains("Make Creator", cut.Markup);
        Assert.Contains("Default Admin Permissions", cut.Markup);
        Assert.Contains("Delete Community", cut.Markup);
        Assert.Contains("Unban", cut.Markup);
    }

    [Fact]
    public void AdminWithManageUsersOnlySeesBanButNotPromoteOrSettings()
    {
        GivenMyRole(CommunityRole.Admin, CommunityPermission.ManageUsers);
        var cut = Render();
        Assert.Contains("Ban", cut.FindAll("button").Select(b => b.TextContent.Trim()));
        Assert.DoesNotContain("Promote to Admin", cut.Markup);
        Assert.DoesNotContain("Default Admin Permissions", cut.Markup);
        Assert.DoesNotContain("Delete Community", cut.Markup);
    }

    [Fact]
    public void AdminWithPromoteSeesPromoteAndEditPermissions()
    {
        GivenMyRole(CommunityRole.Admin, CommunityPermission.PromoteAdmins);
        var cut = Render();
        Assert.Contains("Promote to Admin", cut.Markup);
        Assert.Contains("Edit Permissions", cut.Markup);
        Assert.DoesNotContain("Make Creator", cut.Markup);
    }
}
