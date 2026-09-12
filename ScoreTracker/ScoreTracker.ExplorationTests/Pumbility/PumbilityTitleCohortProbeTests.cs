using MassTransit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Wiring;
using ScoreTracker.ChartIntelligence.Wiring;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.ExplorationTests.Catalog;
using ScoreTracker.Identity.Wiring;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Wiring;
using ScoreTracker.PlayerProgress.Wiring;
using ScoreTracker.ScoreLedger.Wiring;
using ScoreTracker.SharedKernel.Enums;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.Pumbility;

/// <summary>
///     How many players hold each band of each PUMBILITY ladder, and each Phoenix 1 difficulty
///     title — the population the Breakdown card compares a viewer against (D68), and the
///     measurements behind §4.14 and the 25 a level owes before it is read over its gem.
///     <para>
///         Configure <c>CatalogProbe:ConnectionString</c> or SCORETRACKER_CATALOG_CONNECTION.
///         Read-only.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class PumbilityTitleCohortProbeTests
{
    private readonly ITestOutputHelper _output;

    public PumbilityTitleCohortProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [CatalogProbeFact]
    public async Task How_many_players_hold_each_band()
    {
        await using var services = BuildServices();
        var stats = services.GetRequiredService<IPlayerStatsReader>();
        var titles = services.GetRequiredService<ITitleRepository>();
        var snapshots = services.GetRequiredService<IOfficialSnapshotRepository>();
        var token = CancellationToken.None;

        // The board half, once: the sealed snapshot's official rows, and the rows no account claims.
        var snapshot = await snapshots.GetLatestSealed(MixEnum.Phoenix2, token);
        var boards = await snapshots.GetBoards(MixEnum.Phoenix2, token);
        var unlinked = (await snapshots.GetPlayers(MixEnum.Phoenix2, token))
            .Where(p => p.UserId == null).Select(p => p.Id).ToHashSet();
        var placements = snapshot == null
            ? Array.Empty<PlacementRow>()
            : (await snapshots.GetPlacements(snapshot.Id, PlacementScope.OfficialOnly, token)).ToArray();
        _output.WriteLine($"snapshot {snapshot?.Id.ToString() ?? "none"} · {placements.Length} official rows");

        int BoardHolders(PumbilityPool pool, double floor, double? ceiling)
        {
            var name = pool switch
            {
                PumbilityPool.Singles => PumbilityBoards.Singles,
                PumbilityPool.Doubles => PumbilityBoards.Doubles,
                _ => PumbilityBoards.Combined
            };
            var board = boards.FirstOrDefault(b => b.Name == name);
            if (board == null) return 0;
            return placements.Count(p => p.LeaderboardId == board.Id && unlinked.Contains(p.PlayerId)
                                         && (double)p.Score >= floor && (ceiling is not { } top || (double)p.Score < top));
        }

        foreach (var pool in new[] { PumbilityPool.Total, PumbilityPool.Singles, PumbilityPool.Doubles })
        {
            _output.WriteLine($"--- Phoenix 2 · {pool} ---");
            var bands = pool == PumbilityPool.Total
                ? PumbilityBand.Gems().Concat(PumbilityBand.Levels(pool)).ToArray()
                : PumbilityBand.Levels(pool).ToArray();
            var levelSites = new Dictionary<string, int>();
            var levelTotals = new Dictionary<string, int>();
            foreach (var band in bands)
            {
                var site = (await stats.GetPlayersInPoolBand(MixEnum.Phoenix2, pool, band.Floor, band.Ceiling, token))
                    .Count();
                var board = BoardHolders(pool, band.Floor, band.Ceiling);
                if (site + board == 0) continue;
                _output.WriteLine($"{band.Name,-24} {band.Floor,8:N0} site {site,4} + board {board,4} = {site + board,5}");
                levelSites[band.Name.ToString()] = site;
                levelTotals[band.Name.ToString()] = site + board;
            }

            if (pool != PumbilityPool.Total) continue;

            // What "enough" buys: how many accounts keep their own level rather than its gem.
            foreach (var threshold in new[] { 20, 25, 30, 40, 50, 75, 100 })
            {
                var stays = PumbilityBand.Levels(pool)
                    .Where(b => levelTotals.GetValueOrDefault(b.Name.ToString()) >= threshold)
                    .Sum(b => levelSites.GetValueOrDefault(b.Name.ToString()));
                _output.WriteLine($"enough {threshold,4}: {stays,4} accounts stay on their level");
            }
        }

        _output.WriteLine("--- Phoenix 1 · difficulty titles ---");
        foreach (var title in PhoenixTitleList.BuildList().OfType<PhoenixDifficultyTitle>())
        {
            var holders = (await titles.GetUserIdsWithHighestTitle(MixEnum.Phoenix, title.Name, token)).Count();
            if (holders > 0) _output.WriteLine($"{title.Name,-24} lvl {(int)title.Level,3} {holders,5}");
        }

        Assert.True(true, "a measurement, not a guarantee — read the output");
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddMediatR(o => o.RegisterServicesFromAssemblies(
            typeof(CatalogRegistrationExtensions).Assembly,
            typeof(ChartIntelligenceRegistrationExtensions).Assembly,
            typeof(ScoreLedgerRegistrationExtensions).Assembly,
            typeof(PlayerProgressRegistrationExtensions).Assembly,
            typeof(IdentityRegistrationExtensions).Assembly));
        services.AddInfrastructure(new AzureBlobConfiguration(),
            new SqlConfiguration { ConnectionString = CatalogProbeConfiguration.ConnectionString! },
            new SendGridConfiguration());
        services.AddCatalog();
        services.AddScoreLedger();
        services.AddChartIntelligence();
        services.AddPlayerProgress();
        services.AddOfficialMirror();
        services.AddSingleton(Mock.Of<IBus>());
        services.AddSingleton(Mock.Of<ICurrentUserAccessor>());
        return services.BuildServiceProvider();
    }
}
