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
using ScoreTracker.Domain.Models.Titles.Phoenix2;
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
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.Pumbility;

/// <summary>
///     Workshop probe for the PUMBILITY Breakdown card: the page's own frame and breakdown record
///     beside the cohort each pool is compared against (D68) — the band, who holds it, and the
///     level spread with the player on it — so a mock can draw the card exactly as the page would.
///     <para>
///         What the population IS, and the twenty-five a level owes before it is read over its gem,
///         is <see cref="PumbilityTitleCohortProbeTests" />; this one is about one player's card.
///     </para>
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
    public async Task One_players_card_against_the_players_holding_their_title()
    {
        await using var services = BuildServices();
        var mediator = services.GetRequiredService<IMediator>();
        var chartRepository = services.GetRequiredService<IChartRepository>();
        var statsRepository = services.GetRequiredService<IPlayerStatsRepository>();
        const MixEnum mix = MixEnum.Phoenix2;

        var userId = ProbeUserId ??
                     (await statsRepository.GetUserIdsWithStats(mix, CancellationToken.None)).First();
        var charts = (await chartRepository.GetCharts(mix, cancellationToken: CancellationToken.None))
            .ToDictionary(c => c.Id);

        // The frame's reads at Great and the card's cohort, for all three pools.
        var pages = new Dictionary<string, PumbilityPageRecord>();
        var cohorts = new Dictionary<string, PumbilityCohortRecord>();
        foreach (var (key, pool, ladder) in new (string, ChartType?, PumbilityPool)[]
                 {
                     ("All", null, PumbilityPool.Total),
                     ("Single", ChartType.Single, PumbilityPool.Singles),
                     ("Double", ChartType.Double, PumbilityPool.Doubles)
                 })
        {
            pages[key] = await mediator.Send(new GetPumbilityPageQuery(userId, mix, pool, Energy.Great),
                CancellationToken.None);
            cohorts[key] = await mediator.Send(new GetPumbilityTitleCohortQuery(userId, mix, ladder),
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
            ["cohort"] = cohorts
        };

        foreach (var (key, cohort) in cohorts)
        {
            if (cohort.Band is not { } band)
            {
                _output.WriteLine($"{key}: no band — the player stands under this ladder");
                continue;
            }

            var mine = cohort.Spread.Columns.Where(c => c.Mine > 0)
                .Select(c => $"{(c.Type == ChartType.Single ? "S" : "D")}{c.Level}x{c.Mine}");
            _output.WriteLine($"{key}: {band} · {cohort.Holders} holders (board {cohort.BoardHolders}, " +
                              $"as of {cohort.BoardAsOf?.ToString("d MMM") ?? "never swept"}) · " +
                              $"{cohort.Spread.Columns.Count} columns · split {cohort.Split?.Peers.ToString() ?? "none"} · " +
                              $"mine {string.Join(" ", mine)}");
        }

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
        // The page's projection reads the official board for its peers (D59), and the cohort reads
        // it for the players no account claims (D68); nothing here scrapes.
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
