using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Communities.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFCommunitiesRepositoryTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public EFCommunitiesRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // A fresh real MemoryCache per repository keeps the cached community count from
    // leaking between tests (Respawn resets the DB, not the process).
    private EFCommunitiesRepository BuildRepository() =>
        new(_fixture.DbContextFactory, Mock.Of<IPlayerStatsReader>(), Mock.Of<IScoreReader>(),
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IDateTimeOffsetAccessor>(d => d.Now == DateTimeOffset.UtcNow));

    [Fact]
    public async Task SaveCommunityPersistsRolesPermissionsAndBans()
    {
        var owner = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var member = Guid.NewGuid();
        var banned = Guid.NewGuid();

        var community = new Community("Roled", owner, CommunityPrivacyType.Private,
            new[] { owner, admin, member, banned }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), false);
        community.PromoteToAdmin(owner, admin,
            CommunityPermission.ManageInviteLinks | CommunityPermission.ManageUsers);
        community.Ban(owner, banned);
        community.SetDefaultAdminPermissions(owner, CommunityPermission.ManageInviteLinks);
        community.SetDefaultLanguage(owner, "ko");

        await BuildRepository().SaveCommunity(community, CancellationToken.None);
        var retrieved = await BuildRepository().GetCommunityByName("Roled", CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(CommunityRole.Creator, retrieved!.RoleOf(owner));
        Assert.Equal(CommunityRole.Admin, retrieved.RoleOf(admin));
        Assert.Equal(CommunityPermission.ManageInviteLinks | CommunityPermission.ManageUsers,
            retrieved.PermissionsOf(admin));
        Assert.Equal(CommunityRole.Member, retrieved.RoleOf(member));
        Assert.True(retrieved.IsBanned(banned));
        Assert.DoesNotContain(banned, retrieved.MemberIds);
        Assert.Equal(CommunityPermission.ManageInviteLinks, retrieved.DefaultAdminPermissions);
        Assert.Equal("ko", retrieved.DefaultLanguage);
    }

    [Fact]
    public async Task SaveCommunityAndGetCommunityByNameRoundTripPreservesMembersAndInvites()
    {
        var ownerId = Guid.NewGuid();
        var member = Guid.NewGuid();
        var inviteCode = Guid.NewGuid();

        var community = new Community(
            name: "Test Community",
            ownerId: ownerId,
            privacyType: CommunityPrivacyType.PublicWithCode,
            memberIds: new[] { ownerId, member },
            channels: Array.Empty<Community.ChannelConfiguration>(),
            inviteCodes: new Dictionary<Guid, DateOnly?> { [inviteCode] = null },
            isRegional: false);

        await BuildRepository().SaveCommunity(community, CancellationToken.None);

        var retrieved = await BuildRepository().GetCommunityByName("Test Community", CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal("Test Community", (string)retrieved!.Name);
        Assert.Equal(ownerId, retrieved.OwnerId);
        Assert.Equal(CommunityPrivacyType.PublicWithCode, retrieved.PrivacyType);
        Assert.False(retrieved.IsRegional);
        Assert.Equal(2, retrieved.MemberIds.Count);
        Assert.Contains(ownerId, retrieved.MemberIds);
        Assert.Contains(member, retrieved.MemberIds);
        Assert.True(retrieved.InviteCodes.ContainsKey(inviteCode));
    }

    [Fact]
    public async Task GetCommunityByInviteCodeResolvesToTheCommunityName()
    {
        // This is the lookup the join-by-invite flow depends on. If the SQL ever changes
        // (different join shape, schema renames), this catches it.
        var inviteCode = Guid.NewGuid();
        var community = new Community("Invited", Guid.NewGuid(), CommunityPrivacyType.Private,
            Array.Empty<Guid>(), Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?> { [inviteCode] = null }, isRegional: false);

        await BuildRepository().SaveCommunity(community, CancellationToken.None);

        var name = await BuildRepository().GetCommunityByInviteCode(inviteCode, CancellationToken.None);

        Assert.NotNull(name);
        Assert.Equal("Invited", (string)name!);
    }

    [Fact]
    public async Task GetCommunityByInviteCodeReturnsNullForUnknownCode()
    {
        var name = await BuildRepository()
            .GetCommunityByInviteCode(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(name);
    }

    [Fact]
    public async Task SaveCommunityDiffsMembershipReplacingOldRowsWithNew()
    {
        // SaveCommunity computes a diff against existing CommunityMembership rows and replaces them
        // with the new MemberIds set. Verify removed members don't linger.
        var oldOwner = Guid.NewGuid();
        var newOwner = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.SaveCommunity(new Community("MyClub", oldOwner, CommunityPrivacyType.Private,
            new[] { oldOwner }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), false), CancellationToken.None);
        await writer.SaveCommunity(new Community("MyClub", newOwner, CommunityPrivacyType.Public,
            new[] { newOwner }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), true), CancellationToken.None);

        var retrieved = await BuildRepository().GetCommunityByName("MyClub", CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(newOwner, retrieved!.OwnerId);
        Assert.Equal(CommunityPrivacyType.Public, retrieved.PrivacyType);
        Assert.True(retrieved.IsRegional);
        Assert.Single(retrieved.MemberIds);
        Assert.Contains(newOwner, retrieved.MemberIds);
        Assert.DoesNotContain(oldOwner, retrieved.MemberIds);
    }

    [Fact]
    public async Task GetCommunitiesReturnsCommunitiesTheUserIsMemberOf()
    {
        var user = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.SaveCommunity(new Community("Alpha", user, CommunityPrivacyType.Public,
            new[] { user, otherUser }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), false), CancellationToken.None);
        await writer.SaveCommunity(new Community("Beta", user, CommunityPrivacyType.Private,
            new[] { user }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), false), CancellationToken.None);
        await writer.SaveCommunity(new Community("Gamma", otherUser, CommunityPrivacyType.Public,
            new[] { otherUser }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), false), CancellationToken.None);

        var userCommunities = (await BuildRepository().GetCommunities(user, CancellationToken.None)).ToList();

        Assert.Equal(2, userCommunities.Count);
        Assert.Contains(userCommunities, c => (string)c.CommunityName == "Alpha");
        Assert.Contains(userCommunities, c => (string)c.CommunityName == "Beta");
        Assert.DoesNotContain(userCommunities, c => (string)c.CommunityName == "Gamma");
    }

    [Fact]
    public async Task ABanEndsBelongingButNotTheRowTheUnbanMachineryNeeds()
    {
        // The ban RETAINS its membership row to block rejoin, so the two reads deliberately
        // diverge: GetCommunities ("clubs I belong to" — the directory, leaderboard scopes,
        // feeds, rivals audience, recap, comment scopes) drops the club, while GetUserRoles
        // ("rows I hold") keeps it — the roster's Unban button and comment moderation read roles.
        var owner = Guid.NewGuid();
        var banned = Guid.NewGuid();
        var writer = BuildRepository();
        var community = new Community("Clubhouse", owner, CommunityPrivacyType.Public,
            new[] { owner, banned }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), false);
        community.Ban(owner, banned);
        await writer.SaveCommunity(community, CancellationToken.None);

        var belongs = await BuildRepository().GetCommunities(banned, CancellationToken.None);
        var holds = await BuildRepository().GetUserRoles(banned, CancellationToken.None);

        Assert.DoesNotContain(belongs, c => (string)c.CommunityName == "Clubhouse");
        var row = Assert.Single(holds, r => (string)r.CommunityName == "Clubhouse");
        Assert.Equal(CommunityRole.Banned, row.Role);
    }

    [Fact]
    public async Task GetPublicCommunitiesReturnsOnlyPublicAndPublicWithCodeCommunities()
    {
        var owner = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.SaveCommunity(new Community("PublicOne", owner, CommunityPrivacyType.Public,
            new[] { owner }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), false), CancellationToken.None);
        await writer.SaveCommunity(new Community("PublicWithCodeOne", owner, CommunityPrivacyType.PublicWithCode,
            new[] { owner }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), false), CancellationToken.None);
        await writer.SaveCommunity(new Community("PrivateOne", owner, CommunityPrivacyType.Private,
            new[] { owner }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), false), CancellationToken.None);

        var publicCommunities = (await BuildRepository().GetPublicCommunities(CancellationToken.None)).ToList();

        Assert.Equal(2, publicCommunities.Count);
        Assert.Contains(publicCommunities, c => (string)c.CommunityName == "PublicOne");
        Assert.Contains(publicCommunities, c => (string)c.CommunityName == "PublicWithCodeOne");
        Assert.DoesNotContain(publicCommunities, c => (string)c.CommunityName == "PrivateOne");
    }

    // SaveCommunity writes the member projection the caller is holding, so two joins that load
    // the community at the same time each save a set missing the other's row. World is the
    // busiest community on the site and the most exposed to it.
    [Fact]
    public async Task ConcurrentJoinsAllSurvive()
    {
        var owner = Guid.NewGuid();
        await BuildRepository().SaveCommunity(new Community("World", owner, CommunityPrivacyType.Public,
            new[] { owner }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), false), CancellationToken.None);

        var joiners = Enumerable.Range(0, 12).Select(_ => Guid.NewGuid()).ToArray();
        await Task.WhenAll(joiners.Select(id =>
            BuildRepository().AddMembership("World", id, CancellationToken.None)));

        var world = await BuildRepository().GetCommunityByName("World", CancellationToken.None);
        Assert.NotNull(world);
        foreach (var joiner in joiners) Assert.Contains(joiner, world!.MemberIds);
        Assert.Contains(owner, world!.MemberIds);
    }

    /// <summary>
    ///     The community basis of player visibility: every live seat in every user-created community
    ///     you hold a live seat in, keyed by name — World and the regional communities out, banned
    ///     seats out on both sides.
    /// </summary>
    [Fact]
    public async Task GetUserCommunityMembersReadsYourUserCreatedCommunitiesInOneGoWithoutBansOrWorld()
    {
        var me = Guid.NewGuid();
        var mate = Guid.NewGuid();
        var banned = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var repository = BuildRepository();

        var crew = new Community("Crew", me, CommunityPrivacyType.Public, new[] { me, mate, banned },
            Array.Empty<Community.ChannelConfiguration>(), new Dictionary<Guid, DateOnly?>(), false);
        crew.Ban(me, banned);
        await repository.SaveCommunity(crew, CancellationToken.None);
        await repository.SaveCommunity(new Community("Doubles Club", mate, CommunityPrivacyType.Private,
            new[] { mate, me }, Array.Empty<Community.ChannelConfiguration>(), new Dictionary<Guid, DateOnly?>(),
            false), CancellationToken.None);
        // A community you are not in, and the two system communities everyone is in.
        await repository.SaveCommunity(new Community("Elsewhere", stranger, CommunityPrivacyType.Public,
            new[] { stranger, mate }, Array.Empty<Community.ChannelConfiguration>(), new Dictionary<Guid, DateOnly?>(),
            false), CancellationToken.None);
        await repository.SaveCommunity(new Community("World", me, CommunityPrivacyType.Public,
            new[] { me, stranger }, Array.Empty<Community.ChannelConfiguration>(), new Dictionary<Guid, DateOnly?>(),
            false), CancellationToken.None);
        await repository.SaveCommunity(new Community("Narnia", me, CommunityPrivacyType.Public,
            new[] { me, stranger }, Array.Empty<Community.ChannelConfiguration>(), new Dictionary<Guid, DateOnly?>(),
            true), CancellationToken.None);

        var members = await ((ICommunityReader)BuildRepository()).GetUserCommunityMembers(me, CancellationToken.None);

        Assert.Equal(new[] { "Crew", "Doubles Club" }, members.Keys.Select(k => k.ToString()).OrderBy(k => k));
        Assert.Equal(new[] { mate, me }.OrderBy(g => g), members[Name.From("Crew")].OrderBy(g => g));
        Assert.DoesNotContain(banned, members[Name.From("Crew")]);
        Assert.Equal(new[] { mate, me }.OrderBy(g => g), members[Name.From("Doubles Club")].OrderBy(g => g));
    }

    [Fact]
    public async Task AddMembershipIsANoOpForAnExistingMemberOrABannedUser()
    {
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var banned = Guid.NewGuid();
        var community = new Community("Guarded", owner, CommunityPrivacyType.Public,
            new[] { owner, member, banned }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), false);
        community.Ban(owner, banned);
        await BuildRepository().SaveCommunity(community, CancellationToken.None);

        Assert.False(await BuildRepository().AddMembership("Guarded", member, CancellationToken.None));
        Assert.False(await BuildRepository().AddMembership("Guarded", banned, CancellationToken.None));

        var retrieved = await BuildRepository().GetCommunityByName("Guarded", CancellationToken.None);
        Assert.True(retrieved!.IsBanned(banned));
        Assert.DoesNotContain(banned, retrieved.MemberIds);
        Assert.Equal(CommunityRole.Member, retrieved.RoleOf(member));
    }

    [Fact]
    public async Task RemoveMembershipDropsTheMemberButKeepsBansAndTheCreatorSeat()
    {
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var banned = Guid.NewGuid();
        var community = new Community("Leavers", owner, CommunityPrivacyType.Public,
            new[] { owner, member, banned }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), false);
        community.Ban(owner, banned);
        await BuildRepository().SaveCommunity(community, CancellationToken.None);

        await BuildRepository().RemoveMembership("Leavers", member, CancellationToken.None);
        await BuildRepository().RemoveMembership("Leavers", banned, CancellationToken.None);
        await BuildRepository().RemoveMembership("Leavers", owner, CancellationToken.None);

        var retrieved = await BuildRepository().GetCommunityByName("Leavers", CancellationToken.None);
        Assert.DoesNotContain(member, retrieved!.MemberIds);
        Assert.True(retrieved.IsBanned(banned));
        Assert.Equal(CommunityRole.Creator, retrieved.RoleOf(owner));
    }

    /// <summary>
    ///     The peer standing reader counts a community's members through this read, so a ban has
    ///     to leave it the way it leaves every other member read: the row stays (a ban is
    ///     retained so the player cannot rejoin), the player does not.
    /// </summary>
    [Fact]
    public async Task GetMembersLeavesOutTheBanned()
    {
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var banned = Guid.NewGuid();
        var community = new Community("Bouncer", owner, CommunityPrivacyType.Public,
            new[] { owner, member, banned }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), false);
        community.Ban(owner, banned);
        await BuildRepository().SaveCommunity(community, CancellationToken.None);

        ICommunityReader reader = BuildRepository();
        var members = (await reader.GetMembers("Bouncer", CancellationToken.None)).OrderBy(id => id).ToArray();

        Assert.Equal(new[] { owner, member }.OrderBy(id => id), members);
    }
}
