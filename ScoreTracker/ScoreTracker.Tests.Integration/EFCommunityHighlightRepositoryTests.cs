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
///     The community AUDIENCE INDEX, which is all this table is since the win payloads moved to
///     PlayerProgress (docs/design/rivals.md D33). Everything asserted here is about visibility
///     and dedupe; what the wins actually said is PlayerHighlight's business.
/// </summary>
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
                Mock.Of<IScoreReader>(),
                new MemoryCache(new MemoryCacheOptions()),
                Mock.Of<IDateTimeOffsetAccessor>(d => d.Now == Now))
            .SaveCommunity(new Community(name, members.FirstOrDefault(), CommunityPrivacyType.Public,
                    members, Array.Empty<Community.ChannelConfiguration>(), new Dictionary<Guid, DateOnly?>(), false),
                CancellationToken.None);

    [Fact]
    public async Task IndexesAndReadsBackAnEventForACommunityMember()
    {
        var winner = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        await SeedCommunity("Crew", winner, requester);

        await Highlights().AddForUserCommunities(eventId, winner, MixEnum.Phoenix, Now, CancellationToken.None);

        var visible = await Highlights()
            .GetVisibleEventIds(requester, new Name[] { "Crew" }, MixEnum.Phoenix, 20, CancellationToken.None);

        Assert.Equal(eventId, Assert.Single(visible));
    }

    [Fact]
    public async Task ExcludesTheFeedForANonMemberRequester()
    {
        var winner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        await SeedCommunity("Crew", winner);

        await Highlights().AddForUserCommunities(Guid.NewGuid(), winner, MixEnum.Phoenix, Now,
            CancellationToken.None);

        var visible = await Highlights()
            .GetVisibleEventIds(stranger, new Name[] { "Crew" }, MixEnum.Phoenix, 20, CancellationToken.None);

        Assert.Empty(visible);
    }

    [Fact]
    public async Task DedupesAnEventFannedAcrossSeveralSharedCommunities()
    {
        var winner = Guid.NewGuid();
        var requester = Guid.NewGuid();
        await SeedCommunity("Alpha", winner, requester);
        await SeedCommunity("Beta", winner, requester);

        // One event → a row in each community (same EventId); the feed must show it once.
        await Highlights().AddForUserCommunities(Guid.NewGuid(), winner, MixEnum.Phoenix, Now,
            CancellationToken.None);

        var visible = await Highlights()
            .GetVisibleEventIds(requester, new Name[] { "Alpha", "Beta" }, MixEnum.Phoenix, 20,
                CancellationToken.None);

        Assert.Single(visible);
    }

    [Fact]
    public async Task IndexingTheSameEventTwiceWritesNothingNew()
    {
        var winner = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        await SeedCommunity("Crew", winner, requester);

        await Highlights().AddForUserCommunities(eventId, winner, MixEnum.Phoenix, Now, CancellationToken.None);
        await Highlights().AddForUserCommunities(eventId, winner, MixEnum.Phoenix, Now, CancellationToken.None);

        var visible = await Highlights()
            .GetVisibleEventIds(requester, new Name[] { "Crew" }, MixEnum.Phoenix, 20, CancellationToken.None);

        Assert.Single(visible);
    }

    [Fact]
    public async Task PurgeBeforeRemovesRowsOlderThanTheCutoff()
    {
        var winner = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var fresh = Guid.NewGuid();
        await SeedCommunity("Crew", winner, requester);
        await Highlights().AddForUserCommunities(Guid.NewGuid(), winner, MixEnum.Phoenix, Now.AddDays(-40),
            CancellationToken.None);
        await Highlights().AddForUserCommunities(fresh, winner, MixEnum.Phoenix, Now, CancellationToken.None);

        var removed = await Highlights().PurgeBefore(Now.AddDays(-30), CancellationToken.None);

        Assert.Equal(1, removed);
        var visible = await Highlights()
            .GetVisibleEventIds(requester, new Name[] { "Crew" }, MixEnum.Phoenix, 20, CancellationToken.None);
        Assert.Equal(fresh, Assert.Single(visible));
    }

    [Fact]
    public async Task ScopesTheFeedToTheRequestedMix()
    {
        var winner = Guid.NewGuid();
        var requester = Guid.NewGuid();
        await SeedCommunity("Crew", winner, requester);
        await Highlights().AddForUserCommunities(Guid.NewGuid(), winner, MixEnum.Phoenix, Now,
            CancellationToken.None);

        var otherMix = await Highlights()
            .GetVisibleEventIds(requester, new Name[] { "Crew" }, MixEnum.Phoenix2, 20, CancellationToken.None);

        Assert.Empty(otherMix);
    }
}
