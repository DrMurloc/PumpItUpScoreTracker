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
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Wiring;
using ScoreTracker.ScoreLedger.Wiring;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.Pumbility;

/// <summary>
///     The PUMBILITY projection's measurement harness (docs/design/pumbility-overhaul.md §4.8, §9).
///     <para>
///         Two probes against a populated database and one pin that needs none:
///     </para>
///     <list type="bullet">
///         <item>
///             <b>The Phoenix 2 launch backtest.</b> Every Phoenix 2 player, every chart they hold
///             a Phoenix 2 score on, the shipping projector run for them exactly as the page runs
///             it — their own scores never enter their own peer group — and the truth is the score
///             they actually hold. Reported as bias, MAE, how often an SS call was right, and how
///             much of the record the estimator answered at all. Self-selected (players choose
///             what they play), which favours an estimator; the number to watch is not "is it
///             centred" but "where is it not".
///         </item>
///         <item>
///             <b>One player's list</b>, with the peers behind each row — the reproduction that
///             found the 2026-08-13 inflation, kept so the next "this feels high" can be answered
///             the same afternoon.
///         </item>
///         <item>
///             <b>The pin.</b> <see cref="PeerEstimator" /> and this file's own arithmetic agree on
///             fixed inputs, so a change to either shows up here rather than as a silent drift
///             between what was measured and what ships. This one always runs.
///         </item>
///     </list>
///     <para>
///         Configure <c>CatalogProbe:ConnectionString</c> (the shared AppHost user-secrets store),
///         optionally <c>PumbilityProbe:UserId</c> for the list, then
///         <c>dotnet test ScoreTracker/ScoreTracker.ExplorationTests/ScoreTracker.ExplorationTests.csproj --filter "FullyQualifiedName~PumbilityProjection"</c>.
///         Read-only.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class PumbilityProjectionBacktestTests
{
    private const int MinimumLevelForBacktest = 15;
    private readonly ITestOutputHelper _output;

    public PumbilityProjectionBacktestTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ------------------------------------------------------------------ the pin

    [Fact]
    public void The_estimator_and_the_harness_agree_on_fixed_inputs()
    {
        // Phoenix 2: unweighted median, five-peer floor.
        var five = new[] { 940_000, 985_000, 962_000, 990_000, 975_000 };
        Assert.Equal(Harness.Median(five),
            PeerEstimator.Estimate(five.Select(s => new PeerScore(s, 0, 0)).ToArray(), 0,
                PeerEstimator.Phoenix2Quantile, PeerEstimator.Phoenix2MinimumPeers));
        var six = five.Append(999_000).ToArray();
        Assert.Equal(Harness.Median(six),
            PeerEstimator.Estimate(six.Select(s => new PeerScore(s, 0, 0)).ToArray(), 0,
                PeerEstimator.Phoenix2Quantile, PeerEstimator.Phoenix2MinimumPeers));
        Assert.Null(PeerEstimator.Estimate(five.Take(4).Select(s => new PeerScore(s, 0, 0)).ToArray(), 0,
            PeerEstimator.Phoenix2Quantile, PeerEstimator.Phoenix2MinimumPeers));

        // Phoenix 1: growth-weighted 65th percentile, midpoint convention.
        var peers = new[]
        {
            new PeerScore(900_000, 22.0, 19.0), new PeerScore(950_000, 22.0, 22.0),
            new PeerScore(965_000, 22.0, 21.5), new PeerScore(975_000, 22.0, 22.0),
            new PeerScore(985_000, 22.0, 22.0), new PeerScore(990_000, 22.0, 20.0)
        };
        var weighted = peers.Select(p => ((double)p.Score, Math.Exp(-p.Growth))).ToArray();
        Assert.Equal((int)Math.Round(Harness.WeightedQuantile(weighted, 0.65)), PeerEstimator.Estimate(peers));
    }

    // ------------------------------------------------------------------ the backtest

    [CatalogProbeFact]
    public async Task Phoenix2_launch_backtest_against_every_players_actual_scores()
    {
        await using var services = BuildServices();
        var projector = services.GetRequiredService<IScoreProjector>();
        var statsRepository = services.GetRequiredService<IPlayerStatsRepository>();
        var stats = services.GetRequiredService<IPlayerStatsReader>();
        var scores = services.GetRequiredService<IScoreReader>();
        var chartRepository = services.GetRequiredService<IChartRepository>();

        var charts = (await chartRepository.GetCharts(MixEnum.Phoenix2, cancellationToken: CancellationToken.None))
            .ToDictionary(c => c.Id);
        var phoenix1 = (await chartRepository.GetCharts(MixEnum.Phoenix, cancellationToken: CancellationToken.None))
            .Select(c => c.Id).ToHashSet();
        var players = (await statsRepository.GetUserIdsWithStats(MixEnum.Phoenix2, CancellationToken.None)).ToArray();

        var pairs = new List<Pair>();
        var universe = 0;
        var lit = new Dictionary<ChartType, int> { [ChartType.Single] = 0, [ChartType.Double] = 0 };
        foreach (var player in players)
        {
            var mine = (await scores.GetBestScores(MixEnum.Phoenix2, player, CancellationToken.None))
                .Where(s => s is { Score: not null, IsBroken: false } && charts.ContainsKey(s.ChartId))
                .ToArray();
            var playerStats = await stats.GetStats(MixEnum.Phoenix2, player, CancellationToken.None);
            foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
            {
                var level = chartType == ChartType.Single
                    ? playerStats.SinglesCompetitiveLevel
                    : playerStats.DoublesCompetitiveLevel;
                if (level < MinimumLevelForBacktest) continue;

                var held = mine.Where(s => charts[s.ChartId].Type == chartType && (int)charts[s.ChartId].Level >= 10)
                    .ToArray();
                if (held.Length == 0) continue;
                universe += held.Length;

                var projection = await projector.Project(new ScoreProjectionRequest(MixEnum.Phoenix2, chartType,
                    player, held.Select(s => new ProjectionTarget(s.ChartId, (int)charts[s.ChartId].Level)).ToArray(),
                    PeerEstimator.CompetitiveWindow), CancellationToken.None);
                if (projection.Group is { IsLit: true }) lit[chartType]++;

                foreach (var s in held)
                    if (projection.Scores.TryGetValue(s.ChartId, out var estimate))
                        pairs.Add(new Pair(player, chartType, s.ChartId, (int)estimate, (int)s.Score!.Value,
                            !phoenix1.Contains(s.ChartId)));
            }
        }

        _output.WriteLine($"players: {players.Length}; lit for singles {lit[ChartType.Single]}, doubles {lit[ChartType.Double]}");
        _output.WriteLine($"records held (level >= {MinimumLevelForBacktest} players): {universe}; answered: {pairs.Count} ({100.0 * pairs.Count / Math.Max(1, universe):F1}%)");
        Report("all", pairs);
        Report("Phoenix 2 debut charts", pairs.Where(p => p.Debut).ToList());
        Report("charts Phoenix 1 also has", pairs.Where(p => !p.Debut).ToList());
        Report("singles", pairs.Where(p => p.ChartType == ChartType.Single).ToList());
        Report("doubles", pairs.Where(p => p.ChartType == ChartType.Double).ToList());

        Assert.True(true, "a measurement, not a guarantee — read the output");
    }

    // ------------------------------------------------------------------ one player's list

    [CatalogProbeFact]
    public async Task One_players_list_with_the_peers_behind_each_row()
    {
        await using var services = BuildServices();
        var mediator = services.GetRequiredService<IMediator>();
        var statsRepository = services.GetRequiredService<IPlayerStatsRepository>();
        var chartRepository = services.GetRequiredService<IChartRepository>();

        var userId = ProbeUserId ??
                     (await statsRepository.GetUserIdsWithStats(MixEnum.Phoenix2, CancellationToken.None)).First();
        var charts = (await chartRepository.GetCharts(MixEnum.Phoenix2, cancellationToken: CancellationToken.None))
            .ToDictionary(c => c.Id);

        foreach (var pool in new ChartType?[] { null, ChartType.Single, ChartType.Double })
        {
            var projection = await mediator.Send(new ProjectPumbilityGainsQuery(userId, MixEnum.Phoenix2, pool),
                CancellationToken.None);
            _output.WriteLine($"=== {userId} · pool {pool?.ToString() ?? "All"} ===");
            if (projection.Peers != null)
                foreach (var (type, group) in projection.Peers)
                    _output.WriteLine($"  {type}: {group.Kind}, centre {group.Center}, size {group.Size}, pool {group.PoolCount}/{group.PoolSize}, lit {group.IsLit}");
            var rows = projection.ProjectedGains.OrderByDescending(kv => kv.Value).Take(30);
            foreach (var (chartId, gain) in rows)
            {
                var chart = charts[chartId];
                var score = (int)projection.ExpectedScores[chartId];
                _output.WriteLine($"  {chart.Song.Name,-34} {chart.Type.ToString()[0]}{(int)chart.Level,-3} {score,8} {PhoenixScore.From(score).LetterGradeFor(MixEnum.Phoenix2),-8} +{gain:F1}");
            }
        }

        Assert.True(true, "a reproduction, not a guarantee — read the output");
    }

    // ------------------------------------------------------------------ plumbing

    private static Guid? ProbeUserId =>
        Guid.TryParse(Environment.GetEnvironmentVariable("SCORETRACKER_PUMBILITY_PROBE_USER"), out var fromEnv)
            ? fromEnv
            : Guid.TryParse(CatalogProbeConfiguration.Setting("PumbilityProbe:UserId"), out var fromSecrets)
                ? fromSecrets
                : null;

    private void Report(string label, IReadOnlyCollection<Pair> pairs)
    {
        if (pairs.Count == 0)
        {
            _output.WriteLine($"{label}: no pairs");
            return;
        }

        var diffs = pairs.Select(p => (double)(p.Estimate - p.Actual)).OrderBy(d => d).ToArray();
        var ssCalls = pairs.Where(p => p.Estimate >= 980_000).ToArray();
        var ssRight = ssCalls.Count(p => p.Actual >= 980_000);
        _output.WriteLine(
            $"{label,-28} pairs {pairs.Count,5} | bias mean {diffs.Average(),+7:F0} median {Harness.Percentile(diffs, 0.5),+7:F0} " +
            $"| MAE {diffs.Average(Math.Abs),6:F0} | says SS+ {ssCalls.Length,4} ({100.0 * ssCalls.Length / pairs.Count,4:F1}%), right {100.0 * ssRight / Math.Max(1, ssCalls.Length),4:F1}% " +
            $"| >20k high {100.0 * diffs.Count(d => d > 20_000) / pairs.Count,4:F1}%  >20k low {100.0 * diffs.Count(d => d < -20_000) / pairs.Count,4:F1}%");
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
        services.AddTransient<IScoreProjector, ScoreProjector>();
        // The pool read (GetTop50ForPlayerQuery) lives on a saga that also wants a clock, a bus
        // and a current user for its other handlers; none of the three is touched by a read, so
        // plain stand-ins satisfy the constructor. Read-only stays read-only.
        services.AddSingleton<IDateTimeOffsetAccessor>(new SystemClock());
        services.AddSingleton(Mock.Of<IBus>());
        services.AddSingleton(Mock.Of<ICurrentUserAccessor>());
        return services.BuildServiceProvider();
    }

    private sealed class SystemClock : IDateTimeOffsetAccessor
    {
        public DateTimeOffset Now => DateTimeOffset.UtcNow;
    }

    private sealed record Pair(Guid Player, ChartType ChartType, Guid ChartId, int Estimate, int Actual, bool Debut);

    /// <summary>
    ///     The harness's own arithmetic — written independently of <see cref="PeerEstimator" /> on
    ///     purpose, so the pin above is a real equivalence check rather than a call to the same code.
    /// </summary>
    private static class Harness
    {
        public static int Median(IReadOnlyCollection<int> scores)
        {
            var sorted = scores.OrderBy(s => s).Select(s => (double)s).ToArray();
            return (int)Math.Round(Percentile(sorted, 0.5));
        }

        /// <summary>Midpoint-convention percentile over sorted values, equal weights.</summary>
        public static double Percentile(double[] sorted, double q)
        {
            return WeightedQuantile(sorted.Select(v => (v, 1.0)).ToArray(), q);
        }

        /// <summary>Midpoint-convention weighted quantile: position_i = (cum_i − w_i/2) / total.</summary>
        public static double WeightedQuantile((double Value, double Weight)[] weighted, double q)
        {
            var sorted = weighted.OrderBy(w => w.Value).ToArray();
            var total = sorted.Sum(w => w.Weight);
            var positions = new double[sorted.Length];
            var cumulative = 0.0;
            for (var i = 0; i < sorted.Length; i++)
            {
                cumulative += sorted[i].Weight;
                positions[i] = (cumulative - sorted[i].Weight / 2) / total;
            }

            if (q <= positions[0]) return sorted[0].Value;
            if (q >= positions[^1]) return sorted[^1].Value;
            for (var i = 1; i < sorted.Length; i++)
            {
                if (q > positions[i]) continue;
                var t = (q - positions[i - 1]) / (positions[i] - positions[i - 1]);
                return sorted[i - 1].Value + t * (sorted[i].Value - sorted[i - 1].Value);
            }

            return sorted[^1].Value;
        }
    }
}
