using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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

        var failures = await RunEveryPurge(userId);

        Assert.True(failures.Count == 0,
            "These purges throw against a real database, so a deleted account keeps the data they " +
            "were supposed to remove — and every suite that only checks the manifests stays green: " +
            string.Join(" | ", failures));
    }

    /// <summary>
    ///     The row-level half. Every suite above this one runs on mocked ports, and a mock cannot
    ///     over-delete: it records that the handler asked for the right scope, never that the SQL
    ///     honoured it. A repository whose WHERE clause is missing, or keyed to the wrong column,
    ///     passes all of them.
    ///     One probe row per manifest type is planted for a decoy account, with every *other* user
    ///     column on that row pointed at a bystander. Purging two accounts that own none of it must
    ///     move nothing — that is the generic form of the CommunityMembership bug, where the purge
    ///     resolved to a column naming somebody else. Purging the decoy must then take all of it,
    ///     which is the under-deletion half in the same fixture. No per-entity test code, so a new
    ///     table is covered the moment it joins a manifest.
    /// </summary>
    [Fact]
    public async Task APurgeTakesEveryRowItOwnsAndNoRowItDoesNot()
    {
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        var decoy = await seeder.SeedUserAsync();
        var bystander = await seeder.SeedUserAsync();
        var stranger = await seeder.SeedUserAsync();

        var chartId = await seeder.SeedPhoenixChartAsync();
        var boardId = await seeder.SeedMoMBoardAsync();

        var manifest = ManifestTypes();
        var unplantable = new List<string>();
        foreach (var type in manifest)
        {
            var failure = await TryPlant(type, decoy, bystander, chartId, boardId);
            if (failure is not null) unplantable.Add(failure);
        }

        // A type nobody can plant is a type this test silently stops covering, so it fails here
        // rather than quietly shrinking to the empty sweep.
        Assert.True(unplantable.Count == 0,
            "These manifest types could not be given a probe row, so nothing below asserts anything " +
            $"about them: {string.Join(" | ", unplantable)}");

        var planted = await CountRows(manifest);
        var missing = planted.Where(p => p.Value == 0).Select(p => p.Key.Name).ToArray();
        Assert.True(missing.Length == 0, $"Probe rows did not land for: {string.Join(", ", missing)}");

        // The stranger owns nothing at all; the bystander appears only in columns that are not the
        // owning key. Neither purge may move a row.
        foreach (var innocent in new[] { stranger, bystander })
        {
            Assert.Empty(await RunEveryPurge(innocent));
            var after = await CountRows(manifest);
            var lost = after.Where(a => a.Value < planted[a.Key])
                .Select(a => $"{a.Key.Name} {planted[a.Key]}->{a.Value}").ToArray();
            Assert.True(lost.Length == 0,
                "Purging an account that does not own these rows deleted them anyway — the delete is " +
                "keyed to the wrong column, or to no column at all: " + string.Join(", ", lost));
        }

        Assert.Empty(await RunEveryPurge(decoy));

        var remaining = (await CountRows(manifest)).Where(r => r.Value > 0)
            .Select(r => $"{r.Key.Name} ({r.Value})").ToArray();
        Assert.True(remaining.Length == 0,
            "These rows belonged to the purged account and survived, so a deleted account keeps " +
            $"data it was promised would go: {string.Join(", ", remaining)}");
    }

    /// <summary>Every distinct type any vertical declares user-owned.</summary>
    private static IReadOnlyList<Type> ManifestTypes()
    {
        var declared = new List<Type>();
        foreach (var (name, assembly) in Verticals)
        {
            var repository = assembly.GetType($"ScoreTracker.{name}.Infrastructure.EFAccountPurgeRepository");
            var manifest = repository?.GetField("UserOwned", BindingFlags.NonPublic | BindingFlags.Static);
            if (manifest is null) continue;
            declared.AddRange((Type[])manifest.GetValue(null)!);
        }

        return declared.Distinct().ToArray();
    }

    /// <summary>
    ///     One row, owned by <paramref name="ownerId" />, with every other user column set to
    ///     <paramref name="bystanderId" />. Keys get fresh values and non-nullable columns get the
    ///     smallest thing that satisfies them — the row only has to exist and be attributable.
    ///     Values are written through the CLR properties <em>before</em> the entity is tracked: a
    ///     string primary key still null at Add throws there, before any of this could fix it.
    /// </summary>
    private async Task<string?> TryPlant(Type type, Guid ownerId, Guid bystanderId, Guid chartId, Guid boardId)
    {
        try
        {
            await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
            var mapped = database.Model.FindEntityType(type)!;
            var owningColumn = OwningColumn(type, mapped);
            var instance = Activator.CreateInstance(type)!;

            foreach (var property in mapped.GetProperties())
            {
                if (property.PropertyInfo is not { CanWrite: true } slot) continue;
                var clr = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                var current = slot.GetValue(instance);

                // Foreign keys have to point at something real, so the ones the manifests actually
                // reference are seeded once and shared by every probe row.
                if (property.Name == owningColumn) slot.SetValue(instance, ownerId);
                else if (clr == typeof(Guid) && property.Name.EndsWith("UserId", StringComparison.Ordinal))
                    slot.SetValue(instance, bystanderId);
                else if (clr == typeof(Guid) && property.Name == "ChartId") slot.SetValue(instance, chartId);
                else if (clr == typeof(Guid) && property.Name == "MixId")
                    slot.SetValue(instance, TestDataSeeder.PhoenixMixId);
                else if (clr == typeof(Guid) && property.Name == "BoardId") slot.SetValue(instance, boardId);
                else if (property.IsPrimaryKey() && clr == typeof(Guid) &&
                         property.ValueGenerated == ValueGenerated.Never)
                    slot.SetValue(instance, Guid.NewGuid());
                // An initializer can hold a value the column will not take — UserTournamentSession
                // seeds RestTime with TimeSpan.MinValue, which `time` rejects — and default(DateTime)
                // underflows a `datetime` column's 1753 floor. Only touched where one already sits,
                // so a nullable column stays null.
                else if (clr == typeof(TimeSpan) && current is not null) slot.SetValue(instance, TimeSpan.Zero);
                else if (clr == typeof(DateTime) && current is not null) slot.SetValue(instance, Now.UtcDateTime);
                else if (clr == typeof(DateTimeOffset) && current is not null) slot.SetValue(instance, Now);
                else if (property.IsNullable || current is not null) continue;
                else if (clr == typeof(string)) slot.SetValue(instance, "t");
                else if (clr == typeof(byte[])) slot.SetValue(instance, new byte[] { 1 });
            }

            database.Add(instance);
            await database.SaveChangesAsync();
            return null;
        }
        catch (Exception exception)
        {
            return $"{type.Name}: {(exception.InnerException ?? exception).Message}";
        }
    }

    /// <summary>
    ///     The same resolution UserDataPurge performs. Replicated rather than shared on purpose: if
    ///     the two ever disagree the bystander columns above catch it, because a purge keyed to a
    ///     column this says is not the owner deletes a row it was told to leave.
    /// </summary>
    private static string OwningColumn(Type type, IEntityType mapped)
    {
        var declared = type.GetCustomAttribute<PurgeKeyAttribute>()?.PropertyName;
        if (declared is not null) return declared;
        return mapped.GetProperties().Single(p =>
            (p.ClrType == typeof(Guid) || p.ClrType == typeof(Guid?)) &&
            p.Name.EndsWith("UserId", StringComparison.Ordinal)).Name;
    }

    private async Task<Dictionary<Type, int>> CountRows(IEnumerable<Type> types)
    {
        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
        var counts = new Dictionary<Type, int>();
        foreach (var type in types)
        {
            var mapped = database.Model.FindEntityType(type)!;
            var table = $"[{mapped.GetSchema() ?? "scores"}].[{mapped.GetTableName()}]";
            counts[type] = await database.Database
                .SqlQueryRaw<int>($"SELECT COUNT(*) AS Value FROM {table}").SingleAsync();
        }

        return counts;
    }

    /// <summary>Runs every vertical's purge for one user; returns the ones that threw.</summary>
    private async Task<List<string>> RunEveryPurge(Guid userId)
    {
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

        return failures;
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
