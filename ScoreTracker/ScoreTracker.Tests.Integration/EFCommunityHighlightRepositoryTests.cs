using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Infrastructure;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFCommunityHighlightRepositoryTests : IAsyncLifetime
{
    // A fixed instant — never DateTimeOffset.Now in tests. Rows are stamped relative to this.
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;

    public EFCommunityHighlightRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private EFCommunityHighlightRepository Highlights() => new(_fixture.DbContextFactory);

    // Seeds a real Community + memberships through the sibling repo, the same rows the feed joins to.
    private async Task SeedCommunity(string name, params Guid[] members) =>
        await new EFCommunitiesRepository(_fixture.DbContextFactory, Mock.Of<IPlayerStatsReader>(),
                new MemoryCache(new MemoryCacheOptions()))
            .SaveCommunity(new Community(name, members.FirstOrDefault(), CommunityPrivacyType.Public,
                    members, Array.Empty<Community.ChannelConfiguration>(), new Dictionary<Guid, DateOnly?>(), false),
                CancellationToken.None);

    private static SignificantWin Pg(string song) =>
        new(WinKind.NotablePg, ChartId: Guid.NewGuid(), ChartName: song, Difficulty: "S21", RarityShare: 0.004);

    [Fact]
    public async Task PersistsAndReadsBackAWinForACommunityMember()
    {
        var winner = Guid.NewGuid();
        var requester = Guid.NewGuid();
        await SeedCommunity("Crew", winner, requester);

        await Highlights().AddForUserCommunities(Guid.NewGuid(), winner, MixEnum.Phoenix, Now, sessionId: null,
            new[] { Pg("Bee") }, CancellationToken.None);

        var feed = await Highlights()
            .GetForUser(requester, new Name[] { "Crew" }, MixEnum.Phoenix, 20, CancellationToken.None);

        var entry = Assert.Single(feed);
        Assert.Equal(winner, entry.UserId);
        var win = Assert.Single(entry.Wins);
        Assert.Equal(WinKind.NotablePg, win.Kind);
        Assert.Equal("Bee", win.ChartName);
        Assert.Equal(0.004, win.RarityShare);
    }

    [Fact]
    public async Task ExcludesTheFeedForANonMemberRequester()
    {
        var winner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        await SeedCommunity("Crew", winner);

        await Highlights().AddForUserCommunities(Guid.NewGuid(), winner, MixEnum.Phoenix, Now, null,
            new[] { Pg("Bee") }, CancellationToken.None);

        var feed = await Highlights()
            .GetForUser(stranger, new Name[] { "Crew" }, MixEnum.Phoenix, 20, CancellationToken.None);

        Assert.Empty(feed);
    }

    [Fact]
    public async Task DedupesAWinFannedAcrossSeveralSharedCommunities()
    {
        var winner = Guid.NewGuid();
        var requester = Guid.NewGuid();
        await SeedCommunity("Alpha", winner, requester);
        await SeedCommunity("Beta", winner, requester);

        // One event → a row in each community (same EventId); the feed must show it once.
        await Highlights().AddForUserCommunities(Guid.NewGuid(), winner, MixEnum.Phoenix, Now, null,
            new[] { Pg("Bee") }, CancellationToken.None);

        var feed = await Highlights()
            .GetForUser(requester, new Name[] { "Alpha", "Beta" }, MixEnum.Phoenix, 20, CancellationToken.None);

        Assert.Single(feed);
    }

    [Fact]
    public async Task PurgeBeforeRemovesRowsOlderThanTheCutoff()
    {
        var winner = Guid.NewGuid();
        var requester = Guid.NewGuid();
        await SeedCommunity("Crew", winner, requester);
        await Highlights().AddForUserCommunities(Guid.NewGuid(), winner, MixEnum.Phoenix, Now.AddDays(-40), null,
            new[] { Pg("Old") }, CancellationToken.None);
        await Highlights().AddForUserCommunities(Guid.NewGuid(), winner, MixEnum.Phoenix, Now, null,
            new[] { Pg("Fresh") }, CancellationToken.None);

        var removed = await Highlights().PurgeBefore(Now.AddDays(-30), CancellationToken.None);

        Assert.Equal(1, removed);
        var feed = await Highlights()
            .GetForUser(requester, new Name[] { "Crew" }, MixEnum.Phoenix, 20, CancellationToken.None);
        Assert.Equal("Fresh", Assert.Single(feed).Wins.Single().ChartName);
    }

    [Fact]
    public async Task ScopesTheFeedToTheRequestedMix()
    {
        var winner = Guid.NewGuid();
        var requester = Guid.NewGuid();
        await SeedCommunity("Crew", winner, requester);
        await Highlights().AddForUserCommunities(Guid.NewGuid(), winner, MixEnum.Phoenix, Now, null,
            new[] { Pg("Bee") }, CancellationToken.None);

        var otherMix = await Highlights()
            .GetForUser(requester, new Name[] { "Crew" }, MixEnum.Phoenix2, 20, CancellationToken.None);

        Assert.Empty(otherMix);
    }
}
