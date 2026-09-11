using System.Text.Json;
using System.Text.Json.Serialization;
using MassTransit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Wiring;
using ScoreTracker.ChartIntelligence.Wiring;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.ExplorationTests.Catalog;
using ScoreTracker.Identity.Wiring;
using ScoreTracker.OfficialMirror.Wiring;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Wiring;
using ScoreTracker.ScoreLedger.Wiring;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.Pumbility;

/// <summary>
///     Workshop probe for a level-spread chart on the PUMBILITY Breakdown page: for each chart type,
///     how many charts of every level each of the player's PUMBILITY peers keeps in their top 50,
///     beside the page's own frame, breakdown card and comparison record, so a mock can draw the
///     card exactly as the page would. The peers are the page's (the projector's call as the
///     projection saga makes it, board peers included), checked against the saga's own spread.
///     <para>
///         Configure <c>CatalogProbe:ConnectionString</c> or SCORETRACKER_CATALOG_CONNECTION;
///         SCORETRACKER_PUMBILITY_PROBE_USER picks the player; SCORETRACKER_PROBE_OUT names a
///         directory for the mock's JSON. Read-only.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class PumbilityLevelSpreadProbeTests
{
    private readonly ITestOutputHelper _output;

    public PumbilityLevelSpreadProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [CatalogProbeFact]
    public async Task One_players_level_spread_against_their_peers()
    {
        await using var services = BuildServices();
        var mediator = services.GetRequiredService<IMediator>();
        var projector = services.GetRequiredService<IScoreProjector>();
        var chartRepository = services.GetRequiredService<IChartRepository>();
        var statsRepository = services.GetRequiredService<IPlayerStatsRepository>();
        const MixEnum mix = MixEnum.Phoenix2;

        var userId = ProbeUserId ??
                     (await statsRepository.GetUserIdsWithStats(mix, CancellationToken.None)).First();
        var charts = (await chartRepository.GetCharts(mix, cancellationToken: CancellationToken.None))
            .ToDictionary(c => c.Id);
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);

        // The frame's reads at Great and the Breakdown card's comparison, for all three pools.
        var pages = new Dictionary<string, PumbilityPageRecord>();
        var compares = new Dictionary<string, PumbilityPoolCompareRecord>();
        foreach (var (key, pool) in new (string, ChartType?)[]
                     { ("All", null), ("Single", ChartType.Single), ("Double", ChartType.Double) })
        {
            pages[key] = await mediator.Send(new GetPumbilityPageQuery(userId, mix, pool, Energy.Great),
                CancellationToken.None);
            compares[key] = await mediator.Send(new GetPumbilityPoolCompareQuery(userId, mix, pool),
                CancellationToken.None);
        }

        object Entry(PoolEntry e) => new
        {
            place = e.Place,
            name = charts.TryGetValue(e.ChartId, out var c) ? c.Song.Name.ToString() : "?",
            type = charts.TryGetValue(e.ChartId, out var t) ? t.Type.ToString() : "?",
            level = charts.TryGetValue(e.ChartId, out var l) ? (int)l.Level : 0,
            value = e.Value,
            score = (int)e.Score,
            plate = e.Plate?.ToString()
        };

        object? BarOf(PumbilityPageRecord p) =>
            p.BarChartId is { } id && charts.TryGetValue(id, out var c) && p.Pool.Count > 0
                ? new
                {
                    name = c.Song.Name.ToString(),
                    type = c.Type.ToString(),
                    level = (int)c.Level,
                    score = (int)p.Pool[^1].Score,
                    grade = p.Pool[^1].Score.LetterGradeFor(mix).GetName()
                }
                : null;

        var export = new Dictionary<string, object?>
        {
            ["userId"] = userId,
            ["totals"] = pages["All"].Totals,
            ["pages"] = pages.ToDictionary(kv => kv.Key, kv => (object)new
            {
                total = kv.Value.Total,
                count = kv.Value.Pool.Count,
                bar = kv.Value.Bar,
                barChart = BarOf(kv.Value),
                breakdown = kv.Value.Breakdown,
                pool = kv.Value.Pool.Select(Entry).ToArray(),
                waitingRoom = kv.Value.WaitingRoom.Select(Entry).ToArray()
            }),
            ["compare"] = compares
        };

        var floor = Math.Min(pages["Single"].Bar ?? 0, pages["Double"].Bar ?? 0);
        var spread = new Dictionary<string, object>();
        foreach (var type in new[] { ChartType.Single, ChartType.Double })
        {
            var key = type.ToString();
            var page = pages[key];

            // The projection saga's own call (ProjectType), so the peers are the page's: the
            // finish is the pool of the type, and a short one is filled at its weakest chart.
            double? finish = page.Pool.Count == 0 ? null
                : page.Pool.Count >= PeerGroup.PumbilityPoolSize ? page.Total
                : page.Total + (PeerGroup.PumbilityPoolSize - page.Pool.Count) * page.Pool[^1].Value;
            var scoped = charts.Values.Where(c => c.Type == type)
                .Where(c => scoring.GetScore(c, PhoenixScore.Max, PhoenixPlate.PerfectGame, false) > floor)
                .Select(c => new ProjectionTarget(c.Id, (int)c.Level))
                .ToArray();
            var projection = await projector.Project(new ScoreProjectionRequest(mix, type, userId, scoped,
                PeerEstimator.CompetitiveWindow, charts, RelaxFloorWhenEmpty: true, ProjectedTotal: finish,
                ProjectedTotalIsEstimate: page.Pool.Count is > 0 and < PeerGroup.PumbilityPoolSize,
                Quantiles: EnergyRungs.All), CancellationToken.None);
            if (projection.PeerPools is not { } summary || projection.Group is not { IsLit: true } group)
            {
                _output.WriteLine($"{type}: dark, or no pools came back");
                continue;
            }

            var perPeer = summary.Pools.Select(kv => new
            {
                src = kv.Key.UserId != null ? "site" : "board",
                levels = kv.Value.Where(charts.ContainsKey)
                    .GroupBy(id => (int)charts[id].Level)
                    .OrderBy(g => g.Key)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count())
            }).ToArray();

            // The check that these are the page's peers: at every level the saga's spread draws, how
            // many peers keep each count, rebuilt from this summary.
            var countsByPeer = summary.Peers
                .Select(p => summary.Pools.TryGetValue(p, out var held)
                    ? held.Where(charts.ContainsKey).GroupBy(id => (int)charts[id].Level).ToDictionary(g => g.Key, g => g.Count())
                    : new Dictionary<int, int>())
                .ToArray();
            var sagaSpread = compares["All"].Levels.GetValueOrDefault(type);
            var columnsOff = sagaSpread?.Columns.Count(column =>
            {
                var rebuilt = countsByPeer.GroupBy(c => c.GetValueOrDefault(column.Level)).ToDictionary(g => g.Key, g => g.Count());
                return rebuilt.Count != column.PeersByCount.Count ||
                       rebuilt.Any(kv => column.PeersByCount.GetValueOrDefault(kv.Key) != kv.Value);
            });
            var mine = sagaSpread?.Columns.Where(c => c.Mine > 0).ToDictionary(c => c.Level, c => c.Mine) ??
                       new Dictionary<int, int>();

            var poolSizes = summary.Pools.Values.Select(p => p.Count).ToArray();
            _output.WriteLine($"{type}: peers {summary.Peers.Count} (board {group.BoardSize}) · window {group.Lowest:F0}–{group.Highest:F0} · " +
                              $"pool sizes {poolSizes.Min()}–{poolSizes.Max()} · spread columns off from the saga {columnsOff?.ToString() ?? "n/a"} · " +
                              $"mine {string.Join(" ", mine.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}x{kv.Value}"))}");

            spread[key] = new
            {
                peers = summary.Peers.Count,
                board = group.BoardSize,
                boardAsOf = group.BoardAsOf,
                lowest = group.Lowest,
                highest = group.Highest,
                spreadColumnsOffVsSaga = columnsOff,
                mine,
                perPeer
            };
        }

        export["spread"] = spread;

        if (Environment.GetEnvironmentVariable("SCORETRACKER_PROBE_OUT") is { Length: > 0 } outDir)
        {
            Directory.CreateDirectory(outDir);
            var path = Path.Combine(outDir, "level-spread.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(export,
                new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }));
            _output.WriteLine($"exported {path}");
        }

        Assert.True(true, "a measurement, not a guarantee — read the output");
    }

    private static Guid? ProbeUserId =>
        Guid.TryParse(Environment.GetEnvironmentVariable("SCORETRACKER_PUMBILITY_PROBE_USER"), out var fromEnv)
            ? fromEnv
            : Guid.TryParse(CatalogProbeConfiguration.Setting("PumbilityProbe:UserId"), out var fromSecrets)
                ? fromSecrets
                : null;

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
        // The projector reads the official board for its peers (D59); nothing here scrapes.
        services.AddOfficialMirror();
        services.AddTransient<IScoreProjector, ScoreProjector>();
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
