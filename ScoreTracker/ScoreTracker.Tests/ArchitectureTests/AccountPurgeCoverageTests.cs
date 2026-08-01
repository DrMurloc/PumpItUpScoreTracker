using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Catalog.Wiring;
using ScoreTracker.ChartIntelligence.Wiring;
using ScoreTracker.Communities.Wiring;
using ScoreTracker.Data.Persistence;
using ScoreTracker.EventCompetition.Wiring;
using ScoreTracker.HomePage.Wiring;
using ScoreTracker.Identity.Wiring;
using ScoreTracker.OfficialMirror.Wiring;
using ScoreTracker.PlayerProgress.Wiring;
using ScoreTracker.Randomizer.Wiring;
using ScoreTracker.ScoreLedger.Wiring;
using ScoreTracker.WeeklyChallenge.Wiring;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     The purge is the kind of ecosystem that rots quietly: nothing breaks when a new vertical
///     forgets it, and the symptom — data surviving a deletion a player was promised — is
///     invisible until somebody looks (docs/design/delete-my-data.md §15).
/// </summary>
public sealed class AccountPurgeCoverageTests
{
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

    /// <summary>
    ///     Types carrying a user key that no purge deletes, each with the reason it survives.
    ///     Adding a row here is a decision, which is the point of keeping the list.
    /// </summary>
    private static readonly Dictionary<string, string> Exempt = new()
    {
        ["OfficialPlayerEntity"] =
            "Unlinked, not deleted. The row mirrors a public piugame leaderboard entry that " +
            "exists whether we do or not; deleting it would corrupt the mirror.",
        ["CommunityEntity"] =
            "Owning a community blocks account deletion outright (delete-my-data.md §8.2), so " +
            "a purge never meets one — the creator hands it over or deletes it first.",
        ["MergeRequestEntity"] =
            "The merge's own audit trail, retained past the purge it drives. It carries two " +
            "user keys by design (survivor and retired).",
        ["UserEntity"] =
            "Deleted by IAccountPurgeRepository.DeleteUser, last, a week after the purge began.",
        ["AccountDeletionRequestEntity"] =
            "The deletion's own audit trail, retained past the purge it drives — the same standing " +
            "as MergeRequest. The game-tag snapshot it carried is nulled when the purge completes, " +
            "so no personal data outlives the account."
    };

    /// <summary>
    ///     Every type any vertical declares as its own. Attribution does not matter here — only
    ///     that somebody deletes it — so the manifests are read as one set.
    /// </summary>
    private static IReadOnlySet<Type> AllDeclared()
    {
        var declared = new HashSet<Type>();
        foreach (var (name, assembly) in Verticals)
        {
            var repository = assembly.GetType($"ScoreTracker.{name}.Infrastructure.EFAccountPurgeRepository");
            var manifest = repository?.GetField("UserOwned", BindingFlags.NonPublic | BindingFlags.Static);
            if (manifest is null) continue;
            declared.UnionWith((Type[])manifest.GetValue(null)!);
        }

        return declared;
    }

    /// <summary>
    ///     A Guid property whose name ends in UserId. The suffix rather than the exact name is
    ///     what catches Community.OwningUserId, and would catch a CreatedByUserId. An entity
    ///     naming its user column something else entirely is invisible here and has to be added
    ///     by hand — a known limit, not a solved problem.
    /// </summary>
    private static bool IsUserKeyed(Type entity)
    {
        return entity.GetProperties().Any(p =>
            (p.PropertyType == typeof(Guid) || p.PropertyType == typeof(Guid?)) &&
            p.Name.EndsWith("UserId", StringComparison.Ordinal));
    }

    private static IEnumerable<(string Owner, Type Entity)> UserKeyedEntities()
    {
        // ScoreTracker.Data holds Identity's user tables: they predate the vertical split and
        // still hang off the shared context's DbSet properties.
        var sources = Verticals.Append(("Identity(Data)", typeof(ChartAttemptDbContext).Assembly));
        foreach (var (owner, assembly) in sources)
        foreach (var type in assembly.GetTypes())
        {
            if (type is not { IsClass: true, IsAbstract: false }) continue;
            if (type.Namespace?.Contains(".Entities", StringComparison.Ordinal) != true) continue;
            if (IsUserKeyed(type)) yield return (owner, type);
        }
    }

    [Fact]
    public void EveryUserKeyedEntityIsPurgedOrExemptWithAReason()
    {
        var declared = AllDeclared();
        var misses = UserKeyedEntities()
            .Where(e => !declared.Contains(e.Entity) && !Exempt.ContainsKey(e.Entity.Name))
            .Select(e => $"{e.Owner}.{e.Entity.Name}")
            .Distinct()
            .ToArray();

        Assert.True(misses.Length == 0,
            "These tables are keyed to a user and no purge deletes them, so a deleted account " +
            "leaves them behind. Add each to its vertical's UserOwned manifest, or to Exempt " +
            $"with the reason it survives: {string.Join(", ", misses)}");
    }

    [Fact]
    public void MassTransitResolvesEveryVerticalConsumer()
    {
        // The per-vertical tripwires name specific consumer types, so a consumer added to an
        // already-covered vertical slips past them — which is how the purge consumers would
        // have gone unregistered. This asks the question generically instead.
        var services = new ServiceCollection();
        services.AddMassTransit(x =>
        {
            x.AddPlayerProgressConsumers();
            x.AddScoreLedgerConsumers();
            x.AddOfficialMirrorConsumers();
            x.AddChartIntelligenceConsumers();
            x.AddWeeklyChallengeConsumers();
            x.AddEventCompetitionConsumers();
            x.AddCommunitiesConsumers();
            x.AddCatalogConsumers();
            x.AddIdentityConsumers();
            x.AddRandomizerConsumers();
            x.AddHomePageConsumers();
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });

        var registered = services.Select(d => d.ServiceType).ToHashSet();
        var missing = Verticals.SelectMany(v => v.Assembly.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>)))
            .Where(t => !registered.Contains(t))
            .Select(t => t.FullName!)
            .ToArray();

        Assert.True(missing.Length == 0,
            "These bus consumers are never registered, so the messages they handle are silently " +
            "dropped. Add each to its vertical's AddXxxConsumers hook — MassTransit's assembly " +
            $"scan skips internal types: {string.Join(", ", missing)}");
    }
}
