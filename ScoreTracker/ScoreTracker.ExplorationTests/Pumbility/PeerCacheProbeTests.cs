using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Wiring;
using ScoreTracker.ChartIntelligence.Wiring;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ExplorationTests.Catalog;
using ScoreTracker.Identity.Wiring;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Wiring;
using ScoreTracker.PlayerProgress.Wiring;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Wiring;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.Pumbility;

/// <summary>
///     What the two peer-score stores cost and what they save
///     (docs/design/pumbility-overhaul.md §6.14).
///     <para>
///         The PUMBILITY page reads a peer group's scores once per folder and draws several, and
///         the same page asks the mirror who qualifies as a board peer. Before the stores, both of
///         those were SQL on every request; this probe times the same work three ways — straight to
///         the database, the store's first call, and the store warm — so the before and after are
///         measured rather than asserted.
///     </para>
///     <para>
///         Configure <c>CatalogProbe:ConnectionString</c> (the shared AppHost user-secrets store) or
///         the <c>SCORETRACKER_CATALOG_CONNECTION</c> variable, then
///         <c>dotnet test ScoreTracker/ScoreTracker.ExplorationTests/ScoreTracker.ExplorationTests.csproj --filter "FullyQualifiedName~PeerCache"</c>.
///         Read-only.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class PeerCacheProbeTests
{
    /// <summary>A peer band is dozens to hundreds of players (D43); this is the deep end of it.</summary>
    private const int PeerGroupSize = 200;

    /// <summary>The folders a viewer at the top of the ladder actually draws.</summary>
    private static readonly int[] Folders = { 20, 21, 22, 23, 24, 25, 26 };

    private readonly ITestOutputHelper _output;

    public PeerCacheProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [CatalogProbeFact]
    public async Task Peer_reads_before_and_after_the_store()
    {
        await using var services = BuildServices();
        var factory = services.GetRequiredService<IDbContextFactory<ChartAttemptDbContext>>();
        var scores = services.GetRequiredService<IScoreReader>();

        foreach (var mix in new[] { MixEnum.Phoenix2, MixEnum.Phoenix })
        {
            var peers = await BusiestPlayers(factory, mix);
            if (peers.Length == 0)
            {
                _output.WriteLine($"{mix}: no scores, skipped.");
                continue;
            }

            var direct = await Time(() => SweepDirect(factory, mix, peers));
            // The first sweep pays for the whole mix; every one after it is arithmetic.
            var cold = await Time(() => Sweep(scores, mix, peers));
            var warm = await Time(() => Sweep(scores, mix, peers));
            var again = await Time(() => Sweep(scores, mix, peers));

            _output.WriteLine($"{mix}: {peers.Length} peers, {Folders.Length * 2} folders, "
                              + $"{direct.Rows:N0} rows");
            _output.WriteLine($"  straight to the database  {direct.Ms:N0} ms");
            _output.WriteLine($"  store, first call         {cold.Ms:N0} ms  ({cold.Rows:N0} rows)");
            _output.WriteLine($"  store, warm               {warm.Ms:N0} ms, then {again.Ms:N0} ms");
        }
    }

    [CatalogProbeFact]
    public async Task Board_peer_reads_before_and_after_the_store()
    {
        await using var services = BuildServices();
        var official = services.GetRequiredService<IOfficialPlacementReader>();

        // The window a player near the top of the ladder sits in (D53): 500 below, 250 above.
        const double pool = 18_500;
        foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
        {
            var cold = await Time(async () =>
            {
                var reading = await official.GetBoardPeers(MixEnum.Phoenix2, chartType, pool - 500, pool + 250,
                    CancellationToken.None);
                return reading?.Peers.Count ?? 0;
            });
            var warm = await Time(async () =>
            {
                var reading = await official.GetBoardPeers(MixEnum.Phoenix2, chartType, pool - 500, pool + 250,
                    CancellationToken.None);
                return reading?.Peers.Count ?? 0;
            });

            _output.WriteLine($"{chartType}: {cold.Rows} board peers in the window");
            _output.WriteLine($"  first call  {cold.Ms:N0} ms");
            _output.WriteLine($"  warm        {warm.Ms:N0} ms");
        }
    }

    /// <summary>
    ///     What the startup warm-up costs — the whole of both stores, on the background thread
    ///     nobody waits for. The number that matters here is the one a viewer never sees.
    /// </summary>
    [CatalogProbeFact]
    public async Task Warming_both_stores_costs()
    {
        await using var services = BuildServices();
        var mediator = services.GetRequiredService<IMediator>();

        foreach (var mix in new[] { MixEnum.Phoenix2, MixEnum.Phoenix })
        {
            var site = await Time(async () =>
            {
                await mediator.Send(new WarmPeerScoresCommand(mix));
                return 0;
            });
            var board = await Time(async () =>
            {
                await mediator.Send(new WarmBoardScoresCommand(mix));
                return 0;
            });
            _output.WriteLine($"{mix}: site store {site.Ms:N0} ms, board store {board.Ms:N0} ms");
        }
    }

    /// <summary>Every folder both ways round, which is the shape the page draws.</summary>
    private static async Task<int> Sweep(IScoreReader scores, MixEnum mix, Guid[] peers)
    {
        var rows = 0;
        foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
        foreach (var level in Folders)
            rows += (await scores.GetPlayerScoresInLevelRange(mix, peers, chartType,
                DifficultyLevel.From(level), DifficultyLevel.From(level), CancellationToken.None)).Count();

        return rows;
    }

    /// <summary>
    ///     The same sweep as the repository used to run it: one query per folder, the User join
    ///     included. Raw SQL because the record entity is the Ledger's own — a probe has no more
    ///     business with it than any other assembly does.
    /// </summary>
    private static async Task<int> SweepDirect(IDbContextFactory<ChartAttemptDbContext> factory, MixEnum mix,
        Guid[] peers)
    {
        var mixId = MixIds.For(mix);
        var ids = string.Join(",", peers.Select(p => $"'{p}'"));
        var rows = 0;
        foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
        foreach (var level in Folders)
        {
            await using var database = await factory.CreateDbContextAsync();
            rows += (await database.Database.SqlQueryRaw<int>(
                    $"""
                     SELECT COUNT(*) AS Value
                     FROM scores.ChartMix cm
                     JOIN scores.Chart c ON cm.ChartId = c.Id
                     JOIN scores.PhoenixRecord pr ON c.Id = pr.ChartId
                     JOIN scores.[User] u ON pr.UserId = u.Id
                     WHERE cm.MixId = '{mixId}' AND pr.MixId = '{mixId}' AND cm.Level = {level}
                       AND c.Type = '{chartType}' AND pr.Score IS NOT NULL AND pr.IsBroken = 0
                       AND pr.UserId IN ({ids})
                     """)
                .ToArrayAsync())[0];
        }

        return rows;
    }

    /// <summary>The players with the most to say on the mix — the expensive end of a peer group.</summary>
    private static async Task<Guid[]> BusiestPlayers(IDbContextFactory<ChartAttemptDbContext> factory, MixEnum mix)
    {
        var mixId = MixIds.For(mix);
        await using var database = await factory.CreateDbContextAsync();
        return await database.Database.SqlQueryRaw<Guid>(
                $"""
                 SELECT TOP {PeerGroupSize} pr.UserId AS Value
                 FROM scores.PhoenixRecord pr
                 WHERE pr.MixId = '{mixId}' AND pr.Score IS NOT NULL AND pr.IsBroken = 0
                 GROUP BY pr.UserId
                 ORDER BY COUNT(*) DESC
                 """)
            .ToArrayAsync();
    }

    private static async Task<(long Ms, int Rows)> Time(Func<Task<int>> work)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var rows = await work();
        return (started.ElapsedMilliseconds, rows);
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
            typeof(IdentityRegistrationExtensions).Assembly,
            typeof(OfficialMirrorRegistrationExtensions).Assembly));
        services.AddInfrastructure(new AzureBlobConfiguration(),
            new SqlConfiguration { ConnectionString = CatalogProbeConfiguration.ConnectionString! },
            new SendGridConfiguration());
        services.AddCatalog();
        services.AddScoreLedger();
        services.AddChartIntelligence();
        services.AddPlayerProgress();
        services.AddOfficialMirror();
        services.AddSingleton<IDateTimeOffsetAccessor>(new SystemClock());
        services.AddSingleton(Mock.Of<IBus>());
        services.AddSingleton(Mock.Of<ICurrentUserAccessor>());
        return services.BuildServiceProvider();
    }

    private sealed class SystemClock : IDateTimeOffsetAccessor
    {
        public DateTimeOffset Now => DateTimeOffset.UtcNow;
    }
}
