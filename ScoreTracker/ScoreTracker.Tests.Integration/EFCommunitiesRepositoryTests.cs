using ScoreTracker.Communities.Domain;
using ScoreTracker.Communities.Infrastructure;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
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

    // No cache layer in this repo — fresh instance only matters for DbContext lifetime.
    private EFCommunitiesRepository BuildRepository() =>
        new(_fixture.DbContextFactory);

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
}
