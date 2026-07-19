using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class CommunityManagementSagaTests
{
    private readonly Mock<ICommunityRepository> _communities = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();

    private CommunityManagementSaga Build(Guid actingUserId)
    {
        _currentUser.SetupGet(u => u.User).Returns(new UserBuilder().WithId(actingUserId).Build());
        _currentUser.SetupGet(u => u.IsLoggedIn).Returns(true);
        return new CommunityManagementSaga(_communities.Object, _currentUser.Object);
    }

    private void GivenCommunity(Community community)
    {
        _communities.Setup(c => c.GetCommunityByName(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(community);
    }

    private static Community Community(Guid creator, params Guid[] members)
    {
        var ids = new List<Guid> { creator };
        ids.AddRange(members);
        return new Community(Name.From("Acme"), creator, CommunityPrivacyType.Public, ids,
            Array.Empty<Community.ChannelConfiguration>(), new Dictionary<Guid, DateOnly?>(), false);
    }

    [Fact]
    public async Task PromoteMemberSavesTheTargetAsAdmin()
    {
        var creator = Guid.NewGuid();
        var member = Guid.NewGuid();
        var community = Community(creator, member);
        GivenCommunity(community);

        await Build(creator).Handle(
            new PromoteMemberCommand(Name.From("Acme"), member, CommunityPermission.ManageInviteLinks),
            CancellationToken.None);

        _communities.Verify(c => c.SaveCommunity(
            It.Is<Community>(comm => comm.RoleOf(member) == CommunityRole.Admin),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PromoteByAPlainMemberIsRejectedByTheAggregate()
    {
        var creator = Guid.NewGuid();
        var member = Guid.NewGuid();
        var other = Guid.NewGuid();
        GivenCommunity(Community(creator, member, other));

        await Assert.ThrowsAsync<CommunityPermissionException>(() =>
            Build(member).Handle(
                new PromoteMemberCommand(Name.From("Acme"), other, CommunityPermission.ManageInviteLinks),
                CancellationToken.None));
        _communities.Verify(c => c.SaveCommunity(It.IsAny<Community>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BanMemberSavesTheTargetBanned()
    {
        var creator = Guid.NewGuid();
        var member = Guid.NewGuid();
        GivenCommunity(Community(creator, member));

        await Build(creator).Handle(new BanMemberCommand(Name.From("Acme"), member), CancellationToken.None);

        _communities.Verify(c => c.SaveCommunity(
            It.Is<Community>(comm => comm.IsBanned(member) && !comm.MemberIds.Contains(member)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransferOwnershipSavesTheNewCreator()
    {
        var creator = Guid.NewGuid();
        var member = Guid.NewGuid();
        GivenCommunity(Community(creator, member));

        await Build(creator).Handle(new TransferCommunityOwnershipCommand(Name.From("Acme"), member),
            CancellationToken.None);

        _communities.Verify(c => c.SaveCommunity(
            It.Is<Community>(comm => comm.OwnerId == member && comm.RoleOf(creator) == CommunityRole.Admin),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCommunityByTheCreatorDeletes()
    {
        var creator = Guid.NewGuid();
        GivenCommunity(Community(creator));

        await Build(creator).Handle(new DeleteCommunityCommand(Name.From("Acme")), CancellationToken.None);

        _communities.Verify(c => c.DeleteCommunity(It.Is<Name>(n => (string)n == "Acme"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCommunityByANonCreatorThrows()
    {
        var creator = Guid.NewGuid();
        var member = Guid.NewGuid();
        GivenCommunity(Community(creator, member));

        await Assert.ThrowsAsync<CommunityPermissionException>(() =>
            Build(member).Handle(new DeleteCommunityCommand(Name.From("Acme")), CancellationToken.None));
        _communities.Verify(c => c.DeleteCommunity(It.IsAny<Name>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetMyCommunityRoleReturnsTheCallersStanding()
    {
        var creator = Guid.NewGuid();
        GivenCommunity(Community(creator));

        var role = await Build(creator).Handle(new GetMyCommunityRoleQuery(Name.From("Acme")),
            CancellationToken.None);

        Assert.Equal(CommunityRole.Creator, role.Role);
        Assert.Equal(CommunityPermission.All, role.Permissions);
    }

    [Fact]
    public async Task GetMyCommunityRoleReturnsNullRoleForANonMember()
    {
        var creator = Guid.NewGuid();
        GivenCommunity(Community(creator));

        var role = await Build(Guid.NewGuid()).Handle(new GetMyCommunityRoleQuery(Name.From("Acme")),
            CancellationToken.None);

        Assert.Null(role.Role);
    }
}
