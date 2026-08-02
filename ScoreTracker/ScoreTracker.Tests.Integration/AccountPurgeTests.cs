using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Wiring;
using ScoreTracker.ChartIntelligence.Wiring;
using ScoreTracker.Communities.Infrastructure.Entities;
using ScoreTracker.Communities.Wiring;
using ScoreTracker.Data.Persistence;
using ScoreTracker.EventCompetition.Wiring;
using ScoreTracker.HomePage.Wiring;
using ScoreTracker.Identity.Wiring;
using ScoreTracker.OfficialMirror.Wiring;
using ScoreTracker.PlayerProgress.Wiring;
using ScoreTracker.Randomizer.Wiring;
using ScoreTracker.ScoreLedger.Wiring;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;
using ScoreTracker.WeeklyChallenge.Wiring;
using CommunitiesPurge = ScoreTracker.Communities.Infrastructure.EFAccountPurgeRepository;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     AccountPurgeCoverageTests asserts every user-keyed table is <em>named</em> by some vertical's
///     manifest. Naming is not deleting: the manifest is resolved to a column by convention at run
///     time, so a table can be declared, pass that ratchet, and still throw the moment a purge
///     reaches it. That is invisible to every suite that never runs one — which is how
///     CommunityMembership shipped carrying two *UserId columns, a shape UserDataPurge refuses to
///     guess at, against a manifest that listed it first.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class AccountPurgeTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Every vertical assembly, anchored on its public model contribution.</summary>
    private static readonly (string Name, Assembly Assembly)[] Verticals =
    {
        ("ScoreLedger", typeof(ScoreLedgerModelContribution).Assembly),
        ("PlayerProgress", typeof(PlayerProgressModelContribution).Assembly),
        ("ChartIntelligence", typeof(ChartIntelligenceModelContribution).Assembly),
        ("WeeklyChallenge", typeof(WeeklyChallengeModelContribution).Assembly),
        ("EventCompetition", typeof(EventCompetitionModelContribution).Assembly),
        ("Communities", typeof(CommunitiesModelContribution).Assembly),
        ("Randomizer", typeof(RandomizerModelContribution).Assembly),
        ("HomePage", typeof(HomePageModelContribution).Assembly),
        ("Identity", typeof(IdentityModelContribution).Assembly),
        ("Catalog", typeof(CatalogModelContribution).Assembly),
        ("OfficialMirror", typeof(OfficialMirrorModelContribution).Assembly)
    };

    private readonly SqlServerFixture _fixture;

    public AccountPurgeTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    ///     Runs every vertical's purge end to end against the migrated schema. No rows are seeded on
    ///     purpose: UserDataPurge resolves and executes a plan per declared type whether or not that
    ///     type has any, so an empty account still exercises every manifest entry — a type carrying
    ///     no user key or several, one mapped but never migrated, one whose table or column has
    ///     drifted from its ToTable. Seeding would make the test stronger per row and would have to
    ///     be extended by hand for every table any vertical ever adds; this way a new table is
    ///     covered the moment it joins a manifest.
    /// </summary>
    [Fact]
    public async Task EveryVerticalsPurgeRunsAgainstTheRealSchema()
    {
        var userId = await new TestDataSeeder(_fixture.DbContextFactory).SeedUserAsync();

        var failures = new List<string>();
        foreach (var (name, assembly) in Verticals)
        {
            var repositoryType = assembly.GetType($"ScoreTracker.{name}.Infrastructure.EFAccountPurgeRepository");
            if (repositoryType is null) continue;

            var repository = Construct(repositoryType);
            foreach (var purge in PurgeMethods(repositoryType))
                try
                {
                    await (Task)purge.Invoke(repository, new object[] { userId, CancellationToken.None })!;
                }
                catch (Exception exception)
                {
                    var actual = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
                    failures.Add($"{name}.{purge.Name}: {actual.Message}");
                }
        }

        Assert.True(failures.Count == 0,
            "These purges throw against a real database, so a deleted account keeps the data they " +
            "were supposed to remove — and every suite that only checks the manifests stays green: " +
            string.Join(" | ", failures));
    }

    /// <summary>
    ///     CommunityMembership's two user keys mean two different things. UserId is whose seat the
    ///     row is; GrantedByUserId is who handed it over, and it sits on somebody else's row. Keying
    ///     the purge on the wrong one would strip an unrelated member's admin seat because the
    ///     account that promoted them left.
    /// </summary>
    [Fact]
    public async Task PurgingAnAccountTakesItsOwnSeatAndLeavesTheSeatsItGranted()
    {
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        var purged = await seeder.SeedUserAsync();
        var survivor = await seeder.SeedUserAsync();
        var communityId = Guid.NewGuid();

        await using (var seed = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            seed.Set<CommunityMembershipEntity>().AddRange(
                new CommunityMembershipEntity
                {
                    Id = Guid.NewGuid(), CommunityId = communityId, UserId = purged, Role = "Member"
                },
                // The survivor's own admin seat, granted by the account about to be purged.
                new CommunityMembershipEntity
                {
                    Id = Guid.NewGuid(), CommunityId = communityId, UserId = survivor, Role = "Admin",
                    Permissions = 13, GrantedByUserId = purged
                });
            seed.Set<CommunityHighlightEntity>().Add(new CommunityHighlightEntity
            {
                Id = Guid.NewGuid(), EventId = Guid.NewGuid(), CommunityId = communityId, UserId = purged,
                MixId = TestDataSeeder.PhoenixMixId, OccurredAt = Now, Payload = "[]", SchemaVersion = 1
            });
            await seed.SaveChangesAsync();
        }

        await new CommunitiesPurge(_fixture.DbContextFactory).DeleteAllForUser(purged, CancellationToken.None);

        await using var after = await _fixture.DbContextFactory.CreateDbContextAsync();
        var remaining = await after.Set<CommunityMembershipEntity>().ToListAsync();
        var kept = Assert.Single(remaining);
        Assert.Equal(survivor, kept.UserId);
        Assert.Equal("Admin", kept.Role);
        // The grant outlived the granter, so the pointer is cleared rather than the seat revoked.
        Assert.Null(kept.GrantedByUserId);
        Assert.Empty(await after.Set<CommunityHighlightEntity>().ToListAsync());
    }

    /// <summary>
    ///     A membership granted by somebody still on the site is untouched — the null-out above must
    ///     be keyed to the purged account, not applied to the column wholesale.
    /// </summary>
    [Fact]
    public async Task PurgingAnAccountLeavesGrantsMadeByOtherPeopleAlone()
    {
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        var purged = await seeder.SeedUserAsync();
        var granter = await seeder.SeedUserAsync();
        var member = await seeder.SeedUserAsync();

        await using (var seed = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            seed.Set<CommunityMembershipEntity>().Add(new CommunityMembershipEntity
            {
                Id = Guid.NewGuid(), CommunityId = Guid.NewGuid(), UserId = member, Role = "Admin",
                GrantedByUserId = granter
            });
            await seed.SaveChangesAsync();
        }

        await new CommunitiesPurge(_fixture.DbContextFactory).DeleteAllForUser(purged, CancellationToken.None);

        await using var after = await _fixture.DbContextFactory.CreateDbContextAsync();
        var kept = Assert.Single(await after.Set<CommunityMembershipEntity>().ToListAsync());
        Assert.Equal(granter, kept.GrantedByUserId);
    }

    /// <summary>The two-arg purge entry points. DeleteUser is excluded: it drops the User row
    ///     itself, which the saga runs last and separately, and running it here would fight the
    ///     identity data the other manifests are still deleting.</summary>
    private static IEnumerable<MethodInfo> PurgeMethods(Type repositoryType)
    {
        return repositoryType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name != nameof(ChartAttemptDbContext.Dispose) && m.Name != "DeleteUser")
            .Where(m => m.ReturnType == typeof(Task))
            .Where(m => m.GetParameters() is [{ ParameterType: var first }, { ParameterType: var second }] &&
                        first == typeof(Guid) && second == typeof(CancellationToken))
            .OrderBy(m => m.Name, StringComparer.Ordinal);
    }

    private object Construct(Type repositoryType)
    {
        var constructor = repositoryType.GetConstructors().Single();
        var arguments = constructor.GetParameters().Select(object (parameter) =>
        {
            if (parameter.ParameterType == typeof(IDbContextFactory<ChartAttemptDbContext>))
                return _fixture.DbContextFactory;
            if (parameter.ParameterType == typeof(IMemoryCache))
                return new MemoryCache(new MemoryCacheOptions());
            throw new InvalidOperationException(
                $"{repositoryType.Name} takes a {parameter.ParameterType.Name} this test does not know " +
                "how to supply. Add it here — a purge repository the sweep cannot construct is a " +
                "purge the sweep silently stops covering.");
        }).ToArray();
        return constructor.Invoke(arguments);
    }
}
