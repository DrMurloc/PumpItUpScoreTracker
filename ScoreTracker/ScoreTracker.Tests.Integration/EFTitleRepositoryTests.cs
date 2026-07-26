using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Domain.Records;
using ScoreTracker.PlayerProgress.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The title reads against a real SQL Server. The multi-title read is the one that needs a
///     real provider: it translates to an IN over an indexed column, and no in-memory substitute
///     would tell us whether that translated at all rather than silently pulling the table.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFTitleRepositoryTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    private readonly TestDataSeeder _seed;

    public EFTitleRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _seed = new TestDataSeeder(_fixture.DbContextFactory);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private EFTitleRepository BuildRepository() =>
        new(_fixture.DbContextFactory, new MemoryCache(new MemoryCacheOptions()));

    private static TitleAchievedRecord Achieved(Guid userId, string title) =>
        new(userId, title, ParagonLevel.None);

    [Fact]
    public async Task GetUsersWithTitlesReadsEveryNamedTitleInOneGo()
    {
        var climber = await _seed.SeedUserAsync("Climber");
        var standing = await _seed.SeedUserAsync("Standing");
        var repository = BuildRepository();

        await repository.SaveTitles(MixEnum.Phoenix, climber, new[]
        {
            Achieved(climber, "Advanced Lv. 1"),
            Achieved(climber, "Advanced Lv. 2")
        }, CancellationToken.None);
        await repository.SaveTitles(MixEnum.Phoenix, standing, new[]
        {
            Achieved(standing, "Advanced Lv. 1")
        }, CancellationToken.None);

        var rail = await BuildRepository().GetUsersWithTitles(MixEnum.Phoenix,
            new Name[] { "Advanced Lv. 1", "Advanced Lv. 2", "Advanced Lv. 3" }, CancellationToken.None);

        var rows = rail.ToArray();
        Assert.Equal(3, rows.Length);
        Assert.Equal(2, rows.Count(r => r.Title == (Name)"Advanced Lv. 1"));
        Assert.Single(rows.Where(r => r.Title == (Name)"Advanced Lv. 2"));
        // The row carries which title it is, which is what lets a caller tell a player standing
        // on a rung from one who has climbed past it.
        Assert.Equal((Name)"Advanced Lv. 2", rows.Single(r => r.UserId == climber && r.Title == (Name)"Advanced Lv. 2").Title);
    }

    [Fact]
    public async Task GetUsersWithTitlesStaysInsideTheMixItWasAskedFor()
    {
        var user = await _seed.SeedUserAsync("Crossover");
        var repository = BuildRepository();

        await repository.SaveTitles(MixEnum.Phoenix, user,
            new[] { Achieved(user, "Advanced Lv. 1") }, CancellationToken.None);
        await repository.SaveTitles(MixEnum.Phoenix2, user,
            new[] { Achieved(user, "Advanced Lv. 1") }, CancellationToken.None);

        var phoenix = await BuildRepository().GetUsersWithTitles(MixEnum.Phoenix,
            new Name[] { "Advanced Lv. 1" }, CancellationToken.None);

        Assert.Single(phoenix);
    }

    [Fact]
    public async Task AskingForNoTitlesTouchesTheDatabaseNotAtAll()
    {
        var result = await BuildRepository().GetUsersWithTitles(MixEnum.Phoenix,
            Array.Empty<Name>(), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ARailWithNoHoldersComesBackEmptyRatherThanThrowing()
    {
        await _seed.SeedUserAsync("Nobody");

        var result = await BuildRepository().GetUsersWithTitles(MixEnum.Phoenix,
            new Name[] { "Expert Lv. 9", "Expert Lv. 10", "The Master" }, CancellationToken.None);

        Assert.Empty(result);
    }

    /// <summary>Gives a seeded user a game tag — the aggregations only count linked accounts.</summary>
    private async Task LinkGameTagAsync(Guid userId, string gameTag)
    {
        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();
        var user = await context.User.SingleAsync(u => u.Id == userId);
        user.GameTag = gameTag;
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task RarityCountsOnlyPlayersWhoHaveLinkedAGameTag()
    {
        // Both the per-title count and the population it is divided by filter on a linked game
        // tag, so a rarity percentage reads "of players with a linked account" — the two agree,
        // which is the part that matters. An unlinked account counts in neither.
        var linked = await _seed.SeedUserAsync("Linked");
        var alsoLinked = await _seed.SeedUserAsync("AlsoLinked");
        var unlinked = await _seed.SeedUserAsync("Unlinked");
        await LinkGameTagAsync(linked, "LinkedTag");
        await LinkGameTagAsync(alsoLinked, "AlsoLinkedTag");

        var repository = BuildRepository();
        await repository.SaveTitles(MixEnum.Phoenix, linked,
            new[] { Achieved(linked, "The Master"), Achieved(linked, "Advanced Lv. 1") }, CancellationToken.None);
        await repository.SaveTitles(MixEnum.Phoenix, alsoLinked,
            new[] { Achieved(alsoLinked, "Advanced Lv. 1") }, CancellationToken.None);
        await repository.SaveTitles(MixEnum.Phoenix, unlinked,
            new[] { Achieved(unlinked, "Advanced Lv. 1") }, CancellationToken.None);

        var aggregations = (await BuildRepository().GetTitleAggregations(MixEnum.Phoenix, CancellationToken.None))
            .ToDictionary(a => a.Title, a => a.Count);

        Assert.Equal(2, aggregations[(Name)"Advanced Lv. 1"]);
        Assert.Equal(1, aggregations[(Name)"The Master"]);
        Assert.Equal(2, await BuildRepository().CountTitledUsers(CancellationToken.None));
    }
}
