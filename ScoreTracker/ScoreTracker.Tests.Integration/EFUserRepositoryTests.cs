using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Repositories;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFUserRepositoryTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    private readonly TestDataSeeder _seed;

    public EFUserRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _seed = new TestDataSeeder(_fixture.DbContextFactory);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Cache is per-instance; building a fresh repo for the read side guarantees we're seeing
    // database state, not in-process cache state, for ClaimsInvalidatedAt / Feedback / Country lookups.
    private EFUserRepository BuildRepository() =>
        new(_fixture.DbContextFactory, new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task SaveUserPersistsEveryFieldAndGetUserReadsItBack()
    {
        var userId = Guid.NewGuid();
        var claimsTime = new DateTimeOffset(2026, 3, 1, 8, 30, 0, TimeSpan.Zero);
        var user = new User(userId,
            Name: "Alice",
            IsPublic: true,
            GameTag: "AliceTag",
            ProfileImage: new Uri("https://example.invalid/alice.png"),
            Country: "US",
            IsContentLocked: true,
            ClaimsInvalidatedAt: claimsTime);

        await BuildRepository().SaveUser(user);

        var retrieved = await BuildRepository().GetUser(userId);
        Assert.NotNull(retrieved);
        Assert.Equal(userId, retrieved!.Id);
        Assert.Equal("Alice", (string)retrieved.Name);
        Assert.True(retrieved.IsPublic);
        Assert.Equal("AliceTag", (string)retrieved.GameTag!);
        Assert.Equal(new Uri("https://example.invalid/alice.png"), retrieved.ProfileImage);
        Assert.Equal("US", (string)retrieved.Country!);
        Assert.True(retrieved.IsContentLocked);
        Assert.Equal(claimsTime, retrieved.ClaimsInvalidatedAt);
    }

    [Fact]
    public async Task SaveUserUpdatesAnExistingRow()
    {
        var userId = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.SaveUser(new User(userId, "Old", true, null,
            new Uri("https://example.invalid/o.png"), null));
        await writer.SaveUser(new User(userId, "New", false, "Tag",
            new Uri("https://example.invalid/n.png"), "JP"));

        var retrieved = await BuildRepository().GetUser(userId);

        Assert.NotNull(retrieved);
        Assert.Equal("New", (string)retrieved!.Name);
        Assert.False(retrieved.IsPublic);
        Assert.Equal("Tag", (string)retrieved.GameTag!);
        Assert.Equal("JP", (string)retrieved.Country!);
    }

    [Fact]
    public async Task GetUserReturnsNullWhenNoSuchUser()
    {
        var retrieved = await BuildRepository().GetUser(Guid.NewGuid());

        Assert.Null(retrieved);
    }

    [Fact]
    public async Task SearchForUsersByNameMatchesSubstringsCaseInsensitive()
    {
        var writer = BuildRepository();
        await writer.SaveUser(new User(Guid.NewGuid(), "Alice", true, null,
            new Uri("https://example.invalid/a.png"), null));
        await writer.SaveUser(new User(Guid.NewGuid(), "Bob", true, null,
            new Uri("https://example.invalid/b.png"), null));
        await writer.SaveUser(new User(Guid.NewGuid(), "Calice", true, null,
            new Uri("https://example.invalid/c.png"), null));

        var results = (await BuildRepository().SearchForUsersByName("alic")).ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, u => (string)u.Name == "Alice");
        Assert.Contains(results, u => (string)u.Name == "Calice");
    }

    private async Task<Guid> Player(string name, bool isPublic = true, string? gameTag = null)
    {
        var id = Guid.NewGuid();
        await BuildRepository().SaveUser(new User(id, name, isPublic, gameTag,
            new Uri("https://example.invalid/p.png"), null));
        return id;
    }

    /// <summary>
    ///     The site search's ordering, all in SQL: an exact name, then a name that starts with the
    ///     term, then anything containing it, alphabetical inside each rung — which is what makes a
    ///     player called "D" reachable from a single "d".
    /// </summary>
    [Fact]
    public async Task SearchVisibleUsersRanksExactThenPrefixThenContains()
    {
        await Player("Andy");
        await Player("Ed");
        await Player("Dave");
        await Player("D");
        await Player("Bob");

        var hits = await BuildRepository().SearchVisibleUsers("d", 10, Array.Empty<Guid>());

        Assert.Equal(new[] { "D", "Dave", "Andy", "Ed" }, hits.Select(u => (string)u.Name));
    }

    [Fact]
    public async Task SearchVisibleUsersMatchesTheGameTagToo()
    {
        await Player("Zed", gameTag: "ROXY#3");
        await Player("Roxanne");

        var hits = await BuildRepository().SearchVisibleUsers("roxy", 10, Array.Empty<Guid>());

        Assert.Equal(new[] { "Zed" }, hits.Select(u => (string)u.Name));
    }

    [Fact]
    public async Task SearchVisibleUsersFindsAPlayerNamedWithASingleSymbolOrEmoji()
    {
        await Player("!");
        await Player("😀");
        await Player("!Bang");

        var bang = await BuildRepository().SearchVisibleUsers("!", 10, Array.Empty<Guid>());
        var smiley = await BuildRepository().SearchVisibleUsers("😀", 10, Array.Empty<Guid>());

        Assert.Equal(new[] { "!", "!Bang" }, bang.Select(u => (string)u.Name));
        Assert.Equal(new[] { "😀" }, smiley.Select(u => (string)u.Name));
    }

    [Fact]
    public async Task SearchVisibleUsersHidesAPrivatePlayerUnlessTheyAreInTheAllowance()
    {
        var hidden = await Player("Private Pete", isPublic: false);
        await Player("Public Pat");

        var withoutAllowance = await BuildRepository().SearchVisibleUsers("p", 10, Array.Empty<Guid>());
        var withAllowance = await BuildRepository().SearchVisibleUsers("p", 10, new[] { hidden });

        Assert.Equal(new[] { "Public Pat" }, withoutAllowance.Select(u => (string)u.Name));
        Assert.Equal(new[] { "Private Pete", "Public Pat" }, withAllowance.Select(u => (string)u.Name));
    }

    [Fact]
    public async Task SearchVisibleUsersCapsInSqlAndAsksNothingForABlankTerm()
    {
        for (var i = 0; i < 5; i++) await Player($"Cap{i}");

        Assert.Equal(2, (await BuildRepository().SearchVisibleUsers("cap", 2, Array.Empty<Guid>())).Count);
        Assert.Empty(await BuildRepository().SearchVisibleUsers("  ", 10, Array.Empty<Guid>()));
        Assert.Empty(await BuildRepository().SearchVisibleUsers("cap", 0, Array.Empty<Guid>()));
    }

    [Fact]
    public async Task GetUserByExternalLoginResolvesAfterCreateExternalLogin()
    {
        var userId = await _seed.SeedUserAsync(name: "OAuthUser");
        await BuildRepository().CreateExternalLogin(userId, "discord", "external-id-123");

        var retrieved = await BuildRepository().GetUserByExternalLogin("discord", "external-id-123");

        Assert.NotNull(retrieved);
        Assert.Equal(userId, retrieved!.Id);
    }

    [Fact]
    public async Task UiSettingsRoundTripPreservesKeyValuePairsAsJson()
    {
        var userId = await _seed.SeedUserAsync();
        var settings = new Dictionary<string, string>
        {
            ["theme"] = "dark",
            ["PreviousPageCount"] = "42"
        };

        await BuildRepository().SaveUserUiSettings(userId, settings);

        var retrieved = await BuildRepository().GetUserUiSettings(userId);
        Assert.Equal("dark", retrieved["theme"]);
        Assert.Equal("42", retrieved["PreviousPageCount"]);
    }

    [Fact]
    public async Task GetUserByApiTokenReturnsTheLinkedUser()
    {
        var userId = await _seed.SeedUserAsync(name: "TokenUser");
        var token = Guid.NewGuid();
        await BuildRepository().SetUserApiToken(userId, token);

        var retrieved = await BuildRepository().GetUserByApiToken(token);

        Assert.NotNull(retrieved);
        Assert.Equal(userId, retrieved!.Id);
    }

    [Fact]
    public async Task GetUserByApiTokenReturnsNullWhenTokenIsUnknown()
    {
        var retrieved = await BuildRepository().GetUserByApiToken(Guid.NewGuid());

        Assert.Null(retrieved);
    }
}
