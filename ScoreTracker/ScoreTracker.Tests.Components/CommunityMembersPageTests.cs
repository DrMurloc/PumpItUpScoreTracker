using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Commands;
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
        new(id, Name.From(name), new Uri("https://piu.test/avatar.png"), role, permissions, IsPublic: true);

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

    /// <summary>
    ///     A permission the creator cannot tick is a permission no admin can ever hold, and this
    ///     page is the only place any of them are handed out. ManageDiscord shipped missing from
    ///     the list, so every admin sent to /piu link-server was refused by a flag their creator
    ///     had no way to grant.
    /// </summary>
    [Fact]
    public void EveryDelegablePermissionIsOfferedToTheCreator()
    {
        GivenMyRole(CommunityRole.Creator, CommunityPermission.All);
        var cut = Render();

        foreach (var permission in Enum.GetValues<CommunityPermission>()
                     .Where(p => p is not (CommunityPermission.None or CommunityPermission.All)))
            Assert.Contains(PermissionLabels[permission], cut.Markup);
    }

    /// <summary>
    ///     Offered is not the same as grantable — the switch has to reach the command carrying the
    ///     flag. Driven through the promotion defaults rather than the per-admin dialog because a
    ///     MudDialog's body needs a provider this tree has none of; both read the same array, so
    ///     the array is what is under test either way.
    /// </summary>
    [Fact]
    public async Task TickingManageDiscordSendsTheFlagToTheCommand()
    {
        GivenMyRole(CommunityRole.Creator, CommunityPermission.All);
        var cut = Render();

        await cut.FindAll("input[type=checkbox]")
            .Single(input => Label(input)?.Contains("Manage Discord roles") == true)
            .ChangeAsync(new ChangeEventArgs { Value = true });

        // The page dispatches through a shared Dispatch(IRequest ...) helper, so the mock records
        // the call at IRequest — matching on the concrete type here would never bind.
        _mediator.Verify(m => m.Send(
            It.Is<IRequest>(c => c is SetDefaultAdminPermissionsCommand &&
                                 ((SetDefaultAdminPermissionsCommand)c).Permissions
                                 .HasFlag(CommunityPermission.ManageDiscord)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>MudSwitch renders its caption on the wrapping label, not on the input.</summary>
    private static string? Label(IElement input) => input.Closest("label")?.TextContent;

    private static readonly Dictionary<CommunityPermission, string> PermissionLabels = new()
    {
        [CommunityPermission.ManageInviteLinks] = "Manage invite links",
        [CommunityPermission.PromoteAdmins] = "Promote other admins",
        [CommunityPermission.ManageUsers] = "Manage users (ban/unban)",
        [CommunityPermission.ManageChannelSubscriptions] = "Manage channel subscriptions",
        [CommunityPermission.ModerateComments] = "Moderate comments (remove/mute)",
        [CommunityPermission.ManageDiscord] = "Manage Discord roles"
    };
}
