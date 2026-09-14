using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Communities.Infrastructure;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The Discord-role tables against a real provider. This class exists because the repository
///     had none: a shared <c>IQueryable&lt;TRecord&gt;</c> helper whose callers filtered the
///     PROJECTION shipped green through 4,734 mocked tests and threw "could not be translated" on
///     every read — the same trap as EFHardmodeRatingRepository one day earlier. The reads below
///     are here to be EXECUTED; what they return matters less than that they translate at all.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFDiscordRoleRepositoryTests : IAsyncLifetime
{
    private const ulong Guild = 900;
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;

    public EFDiscordRoleRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private EFDiscordRoleRepository Roles() => new(_fixture.DbContextFactory);

    /// <summary>A real Community row, which the designation reads join to for its name.</summary>
    private async Task<Guid> SeedCommunity(string name)
    {
        var owner = Guid.NewGuid();
        var communities = new EFCommunitiesRepository(_fixture.DbContextFactory,
            Mock.Of<IPlayerStatsReader>(), Mock.Of<IScoreReader>(),
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IDateTimeOffsetAccessor>(d => d.Now == Now));

        await communities.SaveCommunity(new Community(Name.From(name), owner,
            CommunityPrivacyType.Public, new[] { owner },
            Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), false), CancellationToken.None);

        var id = await communities.GetCommunityId(Name.From(name), CancellationToken.None);
        Assert.NotNull(id);
        return id!.Value;
    }

    [Fact]
    public async Task ADesignationRoundTripsThroughAllThreeServerReadsCarryingItsCommunityName()
    {
        var communityId = await SeedCommunity("Arrow Eclipse");
        await Roles().SaveServer(communityId, Guild, "The Murloc Den", Now, CancellationToken.None);

        var byCommunity = await Roles().GetServer(communityId, CancellationToken.None);
        var byGuild = await Roles().GetServersByGuild(Guild, CancellationToken.None);
        var all = await Roles().GetAllServers(CancellationToken.None);

        Assert.NotNull(byCommunity);
        Assert.Equal(Guild, byCommunity!.GuildId);
        Assert.Equal("The Murloc Den", byCommunity.GuildName);
        // The name the /piu roles reply prints. A designation with no community row is nothing to
        // sweep, so the join is inner and this is never empty for a row that comes back.
        Assert.Equal("Arrow Eclipse", byCommunity.CommunityName);
        Assert.Equal("Arrow Eclipse", Assert.Single(byGuild).CommunityName);
        Assert.Equal("Arrow Eclipse", Assert.Single(all).CommunityName);
    }

    [Fact]
    public async Task AGuildNobodyDesignatedReadsBackEmptyRatherThanThrowing()
    {
        Assert.Empty(await Roles().GetServersByGuild(Guild, CancellationToken.None));
        Assert.Null(await Roles().GetServer(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task TwoCommunitiesCanDesignateOneGuildAndBothComeBackNamed()
    {
        var korea = await SeedCommunity("Korea");
        var world = await SeedCommunity("World Crew");
        await Roles().SaveServer(korea, Guild, "The Murloc Den", Now, CancellationToken.None);
        await Roles().SaveServer(world, Guild, "The Murloc Den", Now, CancellationToken.None);

        var servers = await Roles().GetServersByGuild(Guild, CancellationToken.None);

        Assert.Equal(new[] { "Korea", "World Crew" },
            servers.Select(s => s.CommunityName).OrderBy(n => n).ToArray());
    }

    // ---- opt-outs ------------------------------------------------------------------------------

    [Fact]
    public async Task AnOptOutIsReadBackForItsCommunityAndNobodyElses()
    {
        var mine = await SeedCommunity("Arrow Eclipse");
        var other = await SeedCommunity("Korea");
        var user = Guid.NewGuid();
        await Roles().SaveOptOut(mine, user, Now, CancellationToken.None);

        Assert.Equal(new[] { user }, (await Roles().GetOptedOutUsers(mine, CancellationToken.None)).ToArray());
        Assert.Empty(await Roles().GetOptedOutUsers(other, CancellationToken.None));
    }

    /// <summary>
    ///     Running the command twice is the player pressing once and the client retrying. The
    ///     unique index refuses the second insert; saying "done" twice is the honest answer.
    /// </summary>
    [Fact]
    public async Task OptingOutTwiceIsNotAnError()
    {
        var communityId = await SeedCommunity("Arrow Eclipse");
        var user = Guid.NewGuid();

        await Roles().SaveOptOut(communityId, user, Now, CancellationToken.None);
        await Roles().SaveOptOut(communityId, user, Now.AddMinutes(5), CancellationToken.None);

        Assert.Single(await Roles().GetOptedOutUsers(communityId, CancellationToken.None));
    }

    [Fact]
    public async Task LiftingAnOptOutReportsWhetherThereWasOneToLift()
    {
        var communityId = await SeedCommunity("Arrow Eclipse");
        var user = Guid.NewGuid();
        await Roles().SaveOptOut(communityId, user, Now, CancellationToken.None);

        Assert.True(await Roles().DeleteOptOut(communityId, user, CancellationToken.None));
        Assert.False(await Roles().DeleteOptOut(communityId, user, CancellationToken.None));
        Assert.Empty(await Roles().GetOptedOutUsers(communityId, CancellationToken.None));
    }

    /// <summary>
    ///     A community that stops existing takes its designation, mappings, grants and opt-outs
    ///     with it — none of it cascades, so without this the rows outlive the club.
    /// </summary>
    [Fact]
    public async Task DeletingEverythingForACommunityTakesTheOptOutsToo()
    {
        var communityId = await SeedCommunity("Arrow Eclipse");
        var user = Guid.NewGuid();
        await Roles().SaveServer(communityId, Guild, "The Murloc Den", Now, CancellationToken.None);
        await Roles().SaveMapping(communityId, "[P.B] GOLD", 5, CancellationToken.None);
        await Roles().SaveOptOut(communityId, user, Now, CancellationToken.None);

        await Roles().DeleteAllForCommunity(communityId, CancellationToken.None);

        Assert.Null(await Roles().GetServer(communityId, CancellationToken.None));
        Assert.Empty(await Roles().GetMappings(communityId, CancellationToken.None));
        Assert.Empty(await Roles().GetOptedOutUsers(communityId, CancellationToken.None));
    }
}
