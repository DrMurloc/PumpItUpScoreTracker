using ScoreTracker.OfficialMirror.Infrastructure.Entities;
using ScoreTracker.OfficialMirror.Infrastructure;
using ScoreTracker.OfficialMirror.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using ScoreTracker.Data.Repositories;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFOfficialLeaderboardRepositoryTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public EFOfficialLeaderboardRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Avatar lookups cache for an hour per instance; fresh cache on each Build() ensures
    // reads exercise the DB path, not whatever the writer cached in process.
    private EFOfficialLeaderboardRepository BuildRepository() =>
        new(_fixture.DbContextFactory,
            NullLogger<EFOfficialLeaderboardRepository>.Instance,
            new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task ClearLeaderboardOnlyTouchesItsOwnMix()
    {
        // Phoenix and Phoenix 2 chart boards share names ("Conflict S22" exists on both
        // sites) — a P2 import's clear must never wipe the Phoenix mirror's rows.
        var writer = BuildRepository();
        await writer.WriteEntries(MixEnum.Phoenix, new[]
        {
            new UserOfficialLeaderboard("alice", 1, "Chart", "Conflict S22", 990000)
        }, CancellationToken.None);
        await writer.WriteEntries(MixEnum.Phoenix2, new[]
        {
            new UserOfficialLeaderboard("byeol", 1, "Chart", "Conflict S22", 995000)
        }, CancellationToken.None);

        await writer.ClearLeaderboard(MixEnum.Phoenix2, "Chart", "Conflict S22", CancellationToken.None);

        var phoenix = (await BuildRepository()
            .GetOfficialLeaderboardStatuses(MixEnum.Phoenix, "alice", CancellationToken.None)).ToList();
        var phoenix2 = (await BuildRepository()
            .GetOfficialLeaderboardStatuses(MixEnum.Phoenix2, "byeol", CancellationToken.None)).ToList();
        Assert.Single(phoenix);
        Assert.Empty(phoenix2);
    }

    [Fact]
    public async Task DeleteWorldRankingsOnlyTouchesItsOwnMix()
    {
        // The world-ranking recalculation deletes-then-rebuilds per mix — a Phoenix 2 run
        // must leave Phoenix's rankings standing.
        var writer = BuildRepository();
        var record = new WorldRankingRecord("alice", "Singles", 20, 950000, 50, 0, 12345, 20.5, 19.4, 18.8);
        await writer.SaveWorldRanking(MixEnum.Phoenix, record, CancellationToken.None);
        await writer.SaveWorldRanking(MixEnum.Phoenix2, record with { Username = "byeol" }, CancellationToken.None);

        await writer.DeleteWorldRankings(MixEnum.Phoenix2, CancellationToken.None);

        Assert.Single(await BuildRepository().GetAllWorldRankings(MixEnum.Phoenix, CancellationToken.None));
        Assert.Empty(await BuildRepository().GetAllWorldRankings(MixEnum.Phoenix2, CancellationToken.None));
    }

    [Fact]
    public async Task UsernameReadsAreScopedToTheirMix()
    {
        var writer = BuildRepository();
        await writer.WriteEntries(MixEnum.Phoenix, new[]
        {
            new UserOfficialLeaderboard("alice", 1, "Chart", "Conflict S22", 990000)
        }, CancellationToken.None);
        await writer.WriteEntries(MixEnum.Phoenix2, new[]
        {
            new UserOfficialLeaderboard("byeol", 1, "Rating", "PUMBILITY", 17418)
        }, CancellationToken.None);

        var phoenixNames = (await BuildRepository()
            .GetOfficialLeaderboardUsernames(MixEnum.Phoenix, null, CancellationToken.None)).ToList();
        var phoenix2Names = (await BuildRepository()
            .GetOfficialLeaderboardUsernames(MixEnum.Phoenix2, null, CancellationToken.None)).ToList();
        Assert.Equal(new[] { "alice" }, phoenixNames);
        Assert.Equal(new[] { "byeol" }, phoenix2Names);
    }

    [Fact]
    public async Task WriteEntryAndGetOfficialLeaderboardStatusesRoundTrip()
    {
        var entry = new UserOfficialLeaderboard("alice", Place: 3, "Rating", "Top Singles", Score: 950);

        await BuildRepository().WriteEntry(MixEnum.Phoenix, entry, CancellationToken.None);

        var retrieved = (await BuildRepository()
            .GetOfficialLeaderboardStatuses(MixEnum.Phoenix, "alice", CancellationToken.None)).ToList();
        Assert.Single(retrieved);
        Assert.Equal("alice", retrieved[0].Username);
        Assert.Equal(3, retrieved[0].Place);
        Assert.Equal("Rating", retrieved[0].OfficialLeaderboardType);
        Assert.Equal("Top Singles", retrieved[0].LeaderboardName);
        Assert.Equal(950, retrieved[0].Score);
    }

    [Fact]
    public async Task ClearLeaderboardRemovesOnlyEntriesMatchingBothTypeAndName()
    {
        var writer = BuildRepository();
        await writer.WriteEntries(MixEnum.Phoenix, new[]
        {
            new UserOfficialLeaderboard("alice", 1, "Rating", "Top Singles", 1000),
            new UserOfficialLeaderboard("bob",   2, "Rating", "Top Singles", 900),
            new UserOfficialLeaderboard("alice", 1, "Rating", "Top Doubles", 850)
        }, CancellationToken.None);

        await writer.ClearLeaderboard(MixEnum.Phoenix, "Rating", "Top Singles", CancellationToken.None);

        var aliceStatuses = (await BuildRepository()
            .GetOfficialLeaderboardStatuses(MixEnum.Phoenix, "alice", CancellationToken.None)).ToList();
        Assert.Single(aliceStatuses);
        Assert.Equal("Top Doubles", aliceStatuses[0].LeaderboardName);
    }

    [Fact]
    public async Task SaveAvatarAndGetUserAvatarsRoundTrip()
    {
        var avatar = new Uri("https://example.invalid/alice-avatar.png");

        await BuildRepository().SaveAvatar("alice", avatar, CancellationToken.None);

        var avatars = (await BuildRepository().GetUserAvatars(CancellationToken.None)).ToList();
        Assert.Single(avatars);
        Assert.Equal("alice", avatars[0].Username);
        Assert.Equal(avatar, avatars[0].AvatarPath);
    }

    [Fact]
    public async Task SaveAvatarOverwritesExistingAvatarForSameUsername()
    {
        var first = new Uri("https://example.invalid/old.png");
        var second = new Uri("https://example.invalid/new.png");
        var writer = BuildRepository();
        await writer.SaveAvatar("alice", first, CancellationToken.None);
        await writer.SaveAvatar("alice", second, CancellationToken.None);

        var avatars = (await BuildRepository().GetUserAvatars(CancellationToken.None)).ToList();

        Assert.Single(avatars);
        Assert.Equal(second, avatars[0].AvatarPath);
    }

    [Fact]
    public async Task LastImportTimestampRoundTripsAndUpdatesTheMixesStateRow()
    {
        var first = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var second = new DateTimeOffset(2026, 5, 14, 0, 0, 0, TimeSpan.Zero);

        await BuildRepository().SetLastImportTimestamp(MixEnum.Phoenix, first, CancellationToken.None);
        var afterFirst = await BuildRepository().GetLastImportTimestamp(MixEnum.Phoenix, CancellationToken.None);

        await BuildRepository().SetLastImportTimestamp(MixEnum.Phoenix, second, CancellationToken.None);
        var afterSecond = await BuildRepository().GetLastImportTimestamp(MixEnum.Phoenix, CancellationToken.None);

        Assert.Equal(first, afterFirst);
        Assert.Equal(second, afterSecond);
    }

    [Fact]
    public async Task LastImportTimestampIsTrackedPerMix()
    {
        // Commit-9 split of the former singleton state row: a Phoenix 2 mirror run (when it
        // is eventually scheduled) must not masquerade as a fresh Phoenix 1 import.
        var phoenixTime = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var phoenix2Time = new DateTimeOffset(2026, 7, 4, 0, 0, 0, TimeSpan.Zero);
        var writer = BuildRepository();

        await writer.SetLastImportTimestamp(MixEnum.Phoenix, phoenixTime, CancellationToken.None);
        Assert.Null(await BuildRepository().GetLastImportTimestamp(MixEnum.Phoenix2, CancellationToken.None));

        await writer.SetLastImportTimestamp(MixEnum.Phoenix2, phoenix2Time, CancellationToken.None);

        Assert.Equal(phoenixTime,
            await BuildRepository().GetLastImportTimestamp(MixEnum.Phoenix, CancellationToken.None));
        Assert.Equal(phoenix2Time,
            await BuildRepository().GetLastImportTimestamp(MixEnum.Phoenix2, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAllAvatarPathsReplacesEntriesWithMatchingOldPathOnly()
    {
        var oldPath = new Uri("https://piu.invalid/alice-v1.png");
        var newPath = new Uri("https://cdn.invalid/alice-v2.png");
        var bobsUntouched = new Uri("https://piu.invalid/bob.png");
        var writer = BuildRepository();
        await writer.SaveAvatar("alice", oldPath, CancellationToken.None);
        await writer.SaveAvatar("bob", bobsUntouched, CancellationToken.None);

        await writer.UpdateAllAvatarPaths(oldPath, newPath, CancellationToken.None);

        var avatars = (await BuildRepository().GetUserAvatars(CancellationToken.None)).ToList();
        var alice = avatars.Single(a => a.Username == "alice");
        var bob = avatars.Single(a => a.Username == "bob");
        Assert.Equal(newPath, alice.AvatarPath);
        Assert.Equal(bobsUntouched, bob.AvatarPath);
    }

    [Fact]
    public async Task SaveWorldRankingAndGetAllWorldRankingsRoundTrip()
    {
        var record = new WorldRankingRecord(
            Username: "alice",
            Type: "Singles",
            AverageDifficulty: 18.5,
            AverageScore: 950000,
            SinglesCount: 50,
            DoublesCount: 10,
            TotalRating: 12345,
            CompetitiveLevel: 19.2,
            SinglesCompetitiveLevel: 19.4,
            DoublesCompetitiveLevel: 18.8);

        await BuildRepository().SaveWorldRanking(MixEnum.Phoenix, record, CancellationToken.None);

        var retrieved = (await BuildRepository().GetAllWorldRankings(MixEnum.Phoenix, CancellationToken.None)).ToList();
        Assert.Single(retrieved);
        Assert.Equal("alice", retrieved[0].Username);
        Assert.Equal("Singles", retrieved[0].Type);
        Assert.Equal(50, retrieved[0].SinglesCount);
        Assert.Equal(12345, retrieved[0].TotalRating);
    }
}
