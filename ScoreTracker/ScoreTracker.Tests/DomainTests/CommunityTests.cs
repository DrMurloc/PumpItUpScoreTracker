using System;
using System.Collections.Generic;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class CommunityTests
{
    private static readonly Guid Creator = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly Guid Member = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();

    private static Community WithMembers(params Guid[] members)
    {
        var community = new Community(Name.From("Test"), Creator, CommunityPrivacyType.Public, false);
        community.MemberIds.Add(Creator);
        foreach (var member in members) community.MemberIds.Add(member);
        return community;
    }

    [Fact]
    public void CreatorHoldsEveryPermission()
    {
        var community = WithMembers();
        Assert.Equal(CommunityRole.Creator, community.RoleOf(Creator));
        Assert.Equal(CommunityPermission.All, community.PermissionsOf(Creator));
        Assert.True(community.HasPermission(Creator, CommunityPermission.PromoteAdmins));
    }

    [Fact]
    public void PlainMemberHoldsNoPermission()
    {
        var community = WithMembers(Member);
        Assert.Equal(CommunityRole.Member, community.RoleOf(Member));
        Assert.Equal(CommunityPermission.None, community.PermissionsOf(Member));
    }

    [Fact]
    public void CreatorCanPromoteMemberToAdmin()
    {
        var community = WithMembers(Member);
        community.PromoteToAdmin(Creator, Member, CommunityPermission.ManageInviteLinks);
        Assert.Equal(CommunityRole.Admin, community.RoleOf(Member));
        Assert.Equal(CommunityPermission.ManageInviteLinks, community.PermissionsOf(Member));
    }

    [Fact]
    public void PromotingANonMemberThrows()
    {
        var community = WithMembers();
        Assert.Throws<CommunityPermissionException>(() =>
            community.PromoteToAdmin(Creator, Stranger, CommunityPermission.ManageInviteLinks));
    }

    [Fact]
    public void MemberWithoutPromotePermissionCannotPromote()
    {
        var community = WithMembers(Admin, Member);
        community.PromoteToAdmin(Creator, Admin, CommunityPermission.ManageUsers); // no PromoteAdmins
        Assert.Throws<CommunityPermissionException>(() =>
            community.PromoteToAdmin(Admin, Member, CommunityPermission.ManageUsers));
    }

    [Fact]
    public void AdminCanOnlyGrantPermissionsItHolds()
    {
        var community = WithMembers(Admin, Member);
        // Admin may promote, but does not hold ManageUsers — cannot pass it on.
        community.PromoteToAdmin(Creator, Admin, CommunityPermission.PromoteAdmins);
        Assert.Throws<CommunityPermissionException>(() =>
            community.PromoteToAdmin(Admin, Member, CommunityPermission.ManageUsers));
        // But it may grant what it holds.
        community.PromoteToAdmin(Admin, Member, CommunityPermission.PromoteAdmins);
        Assert.Equal(CommunityRole.Admin, community.RoleOf(Member));
    }

    [Fact]
    public void DemoteReturnsAdminToMember()
    {
        var community = WithMembers(Admin);
        community.PromoteToAdmin(Creator, Admin, CommunityPermission.ManageInviteLinks);
        community.DemoteToMember(Creator, Admin);
        Assert.Equal(CommunityRole.Member, community.RoleOf(Admin));
    }

    [Fact]
    public void BanRemovesMembershipAndBlocksRejoin()
    {
        var community = WithMembers(Member);
        community.Ban(Creator, Member);
        Assert.Equal(CommunityRole.Banned, community.RoleOf(Member));
        Assert.True(community.IsBanned(Member));
        Assert.DoesNotContain(Member, community.MemberIds);
        Assert.Contains(Member, community.BannedUserIds);
    }

    [Fact]
    public void CreatorCannotBeBanned()
    {
        var community = WithMembers(Admin);
        community.PromoteToAdmin(Creator, Admin, CommunityPermission.ManageUsers);
        Assert.Throws<CommunityPermissionException>(() => community.Ban(Admin, Creator));
    }

    [Fact]
    public void BanRequiresManageUsers()
    {
        var community = WithMembers(Admin, Member);
        community.PromoteToAdmin(Creator, Admin, CommunityPermission.ManageInviteLinks);
        Assert.Throws<CommunityPermissionException>(() => community.Ban(Admin, Member));
    }

    [Fact]
    public void UnbanLiftsTheBlock()
    {
        var community = WithMembers(Member);
        community.Ban(Creator, Member);
        community.Unban(Creator, Member);
        Assert.False(community.IsBanned(Member));
        Assert.Null(community.RoleOf(Member));
    }

    [Fact]
    public void TransferCreatorIsASingleSeatSwap()
    {
        var community = WithMembers(Member);
        community.TransferCreator(Creator, Member);
        Assert.Equal(CommunityRole.Creator, community.RoleOf(Member));
        Assert.Equal(Member, community.OwnerId);
        // The old creator is demoted to an admin holding all permissions.
        Assert.Equal(CommunityRole.Admin, community.RoleOf(Creator));
        Assert.Equal(CommunityPermission.All, community.PermissionsOf(Creator));
    }

    [Fact]
    public void OnlyCreatorCanTransfer()
    {
        var community = WithMembers(Admin, Member);
        community.PromoteToAdmin(Creator, Admin, CommunityPermission.All);
        Assert.Throws<CommunityPermissionException>(() => community.TransferCreator(Admin, Member));
    }

    [Fact]
    public void TransferToANonMemberThrows()
    {
        var community = WithMembers();
        Assert.Throws<CommunityPermissionException>(() => community.TransferCreator(Creator, Stranger));
    }

    [Fact]
    public void CreatorOnlySettingsRejectNonCreators()
    {
        var community = WithMembers(Admin);
        community.PromoteToAdmin(Creator, Admin, CommunityPermission.All);
        Assert.Throws<CommunityPermissionException>(() =>
            community.SetPrivacy(Admin, CommunityPrivacyType.Private));
        Assert.Throws<CommunityPermissionException>(() =>
            community.SetDefaultLanguage(Admin, "ko"));
        Assert.Throws<CommunityPermissionException>(() =>
            community.SetDefaultAdminPermissions(Admin, CommunityPermission.None));

        community.SetPrivacy(Creator, CommunityPrivacyType.Private);
        Assert.Equal(CommunityPrivacyType.Private, community.PrivacyType);
    }

    [Fact]
    public void RegionalCommunityRejectsAllManagement()
    {
        // Regional communities are ownerless (Guid.Empty) — no creator to authorize anything.
        var regional = new Community(Name.From("World"), Guid.Empty, CommunityPrivacyType.Public, true);
        regional.MemberIds.Add(Member);
        Assert.Throws<CommunityPermissionException>(() =>
            regional.PromoteToAdmin(Member, Member, CommunityPermission.All));
        Assert.Throws<CommunityPermissionException>(() =>
            regional.SetPrivacy(Member, CommunityPrivacyType.Private));
    }

    [Fact]
    public void DefaultAdminPermissionsSeedExcludesPromote()
    {
        var community = WithMembers();
        Assert.Equal(Community.DefaultAdminPermissionsSeed, community.DefaultAdminPermissions);
        Assert.False(community.DefaultAdminPermissions.HasFlag(CommunityPermission.PromoteAdmins));
        Assert.True(community.DefaultAdminPermissions.HasFlag(CommunityPermission.ManageInviteLinks));
    }

    [Fact]
    public void HydrationConstructorRestoresRolesAndBans()
    {
        var members = new[]
        {
            new CommunityMember(Creator, CommunityRole.Creator, CommunityPermission.All, null, null),
            new CommunityMember(Admin, CommunityRole.Admin, CommunityPermission.ManageInviteLinks, Creator, null),
            new CommunityMember(Member, CommunityRole.Member, CommunityPermission.None, null, null),
            new CommunityMember(Stranger, CommunityRole.Banned, CommunityPermission.None, null, null)
        };
        var community = new Community(Name.From("Test"), Creator, CommunityPrivacyType.Private, members,
            Array.Empty<Community.ChannelConfiguration>(), new Dictionary<Guid, DateOnly?>(), false,
            CommunityPermission.ManageInviteLinks, "ko");

        Assert.Equal(CommunityRole.Creator, community.RoleOf(Creator));
        Assert.Equal(CommunityRole.Admin, community.RoleOf(Admin));
        Assert.Equal(CommunityPermission.ManageInviteLinks, community.PermissionsOf(Admin));
        Assert.Equal(CommunityRole.Member, community.RoleOf(Member));
        Assert.True(community.IsBanned(Stranger));
        Assert.DoesNotContain(Stranger, community.MemberIds);
        Assert.Equal("ko", community.DefaultLanguage);
        Assert.Equal(CommunityPermission.ManageInviteLinks, community.DefaultAdminPermissions);
    }
}
