using ScoreTracker.Catalog.Wiring;
using ScoreTracker.ChartIntelligence.Wiring;
using ScoreTracker.Communities.Wiring;
using ScoreTracker.CompositionRoot;
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

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     A vertical missing from VerticalModelContributions.All() has its tables silently dropped
///     from scaffolded migrations — the kind of failure that surfaces as a missing table in a
///     fresh environment weeks later. This lives here rather than in ScoreTracker.Tests because
///     CompositionRoot is only referenced by this project; it needs no database of its own.
/// </summary>
public sealed class ModelContributionRegistrationTests
{
    [Fact]
    public void EveryModelContributionIsRegistered()
    {
        var assemblies = new[]
        {
            typeof(ScoreLedgerModelContribution).Assembly,
            typeof(PlayerProgressModelContribution).Assembly,
            typeof(ChartIntelligenceModelContribution).Assembly,
            typeof(WeeklyChallengeModelContribution).Assembly,
            typeof(EventCompetitionModelContribution).Assembly,
            typeof(CommunitiesModelContribution).Assembly,
            typeof(RandomizerModelContribution).Assembly,
            typeof(HomePageModelContribution).Assembly,
            typeof(IdentityModelContribution).Assembly,
            typeof(CatalogModelContribution).Assembly,
            typeof(OfficialMirrorModelContribution).Assembly
        }.Distinct();

        var registered = VerticalModelContributions.All().Select(c => c.GetType()).ToHashSet();
        var missing = assemblies.SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => typeof(IDbModelContribution).IsAssignableFrom(t))
            .Where(t => !registered.Contains(t))
            .Select(t => t.FullName!)
            .ToArray();

        Assert.True(missing.Length == 0,
            "These model contributions are not listed in VerticalModelContributions.All(), so " +
            "their tables drop out of every scaffolded migration and out of any environment " +
            $"built from scratch: {string.Join(", ", missing)}");
    }
}
