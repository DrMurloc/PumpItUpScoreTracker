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
using ScoreTracker.SharedKernel.Models;
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

                // The catalog rides along so the peers' voices per chart come back with the estimate
                // (PeerPools) — the personal-quantile experiment below needs where each actual score
                // sat among the peers, not just the three quantiles.
                var projection = await projector.Project(new ScoreProjectionRequest(MixEnum.Phoenix2, chartType,
                    player, held.Select(s => new ProjectionTarget(s.ChartId, (int)charts[s.ChartId].Level)).ToArray(),
                    PeerEstimator.CompetitiveWindow, charts), CancellationToken.None);
                if (projection.Group is { IsLit: true }) lit[chartType]++;

                foreach (var s in held)
                    if (projection.Scores.TryGetValue(s.ChartId, out var estimate))
                        pairs.Add(Pair.Of(player, chartType, s.ChartId, (int)estimate, (int)s.Score!.Value,
                            !phoenix1.Contains(s.ChartId), projection.Spreads,
                            projection.PeerPools?.Charts.GetValueOrDefault(s.ChartId)?.Scores));
            }
        }

        _output.WriteLine($"players: {players.Length}; lit for singles {lit[ChartType.Single]}, doubles {lit[ChartType.Double]}");
        _output.WriteLine($"records held (level >= {MinimumLevelForBacktest} players): {universe}; answered: {pairs.Count} ({100.0 * pairs.Count / Math.Max(1, universe):F1}%)");
        Report("all", pairs, MixEnum.Phoenix2);
        Report("Phoenix 2 debut charts", pairs.Where(p => p.Debut).ToList(), MixEnum.Phoenix2);
        Report("charts Phoenix 1 also has", pairs.Where(p => !p.Debut).ToList(), MixEnum.Phoenix2);
        Report("singles", pairs.Where(p => p.ChartType == ChartType.Single).ToList(), MixEnum.Phoenix2);
        Report("doubles", pairs.Where(p => p.ChartType == ChartType.Double).ToList(), MixEnum.Phoenix2);
        TopOfList(pairs, charts, MixEnum.Phoenix2);
        PersonalQuantile(pairs, MixEnum.Phoenix2);

        Assert.True(true, "a measurement, not a guarantee — read the output");
    }

    /// <summary>
    ///     What a player actually sees is the top of a list sorted by projected gain, and sorting by
    ///     an estimate selects for the charts where that estimate ran high — an estimator that is
    ///     centered over everything still hands you a biased top ten. Per player-type, rank the
    ///     answered charts by their projected PUMBILITY value at a quantile (the bar is one number
    ///     within a player, so this is the page's own order) and read the top ten at that same
    ///     quantile, against the rest of the list. Each quantile ranks its own list, because the
    ///     page would.
    /// </summary>
    private void TopOfList(IReadOnlyCollection<Pair> pairs, IReadOnlyDictionary<Guid, Chart> charts, MixEnum mix)
    {
        const int listTop = 10;
        const int minimumList = 20;
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);
        double Value(Pair p, int score)
        {
            var phoenix = PhoenixScore.From(score);
            return scoring.GetScore(charts[p.ChartId], phoenix, ScoringConfiguration.ExpectedPlateForScore(phoenix), false);
        }

        var shipping = mix == MixEnum.Phoenix2 ? "p50" : "p65";
        var reads = new (string Label, Func<Pair, int?> At)[]
        {
            (shipping, p => p.Estimate), ("p25", p => p.Q1), ("p75", p => p.Q3)
        };
        var lists = 0;
        foreach (var (label, at) in reads)
        {
            var top = new List<Pair>();
            var rest = new List<Pair>();
            foreach (var group in pairs.Where(p => at(p) != null).GroupBy(p => (p.Player, p.ChartType)))
            {
                var ordered = group.OrderByDescending(p => Value(p, at(p)!.Value)).ToArray();
                if (ordered.Length < minimumList) continue;
                top.AddRange(ordered.Take(listTop));
                rest.AddRange(ordered.Skip(listTop));
            }

            if (label == shipping) lists = top.Count / listTop;
            if (top.Count == 0) continue;
            Row($"top {listTop} of the list · {label}", top, p => at(p)!.Value, mix);
            Row($"the rest of the list · {label}", rest, p => at(p)!.Value, mix);
        }

        _output.WriteLine($"  (lists: {lists} player-types with >= {minimumList} answered charts; each quantile ranks its own list)");
    }

    /// <summary>
    ///     Could the quantile be the player's own rather than one number for everyone? Split-half:
    ///     each player-type's answered charts are ordered by id, the odd half fits "where do my
    ///     scores sit among my peers" (the median of the actual score's percentile among the peers'
    ///     voices on each chart), and the even half is scored at that quantile — against p50 and p25
    ///     on the very same pairs. A shrunk variant halves the distance from the median, the usual
    ///     hedge for a personal estimate fitted on a handful of charts. §4.5 measured a per-player
    ///     OFFSET on Phoenix 1 as +9.1% worse — self-selection — so this asks the same question of
    ///     a per-player QUANTILE on Phoenix 2 before anyone proposes shipping one.
    /// </summary>
    private void PersonalQuantile(IReadOnlyCollection<Pair> pairs, MixEnum mix)
    {
        const int minimumFit = 6;
        var fitted = new List<double>();
        var consistency = new List<double>();
        var eval = new List<(Pair Pair, double Q, double Shrunk)>();
        foreach (var group in pairs.Where(p => p.Percentile != null).GroupBy(p => (p.Player, p.ChartType)))
        {
            var ordered = group.OrderBy(p => p.ChartId).ToArray();
            var fit = ordered.Where((_, i) => i % 2 == 1).Select(p => p.Percentile!.Value).OrderBy(x => x).ToArray();
            var hold = ordered.Where((_, i) => i % 2 == 0).ToArray();
            if (fit.Length < minimumFit || hold.Length == 0) continue;
            var q = Math.Clamp(Harness.Percentile(fit, 0.5), 0.05, 0.95);
            fitted.Add(q);
            consistency.Add(Harness.Percentile(fit, 0.75) - Harness.Percentile(fit, 0.25));
            foreach (var p in hold) eval.Add((p, q, 0.5 + 0.5 * (q - 0.5)));
        }

        if (fitted.Count == 0)
        {
            _output.WriteLine("personal quantile: too few pairs per player to fit");
            return;
        }

        var qs = fitted.OrderBy(x => x).ToArray();
        _output.WriteLine(
            $"personal quantile, fit half ({fitted.Count} player-types with >= {minimumFit} charts): " +
            $"p10 {Harness.Percentile(qs, 0.10):F2} p25 {Harness.Percentile(qs, 0.25):F2} p50 {Harness.Percentile(qs, 0.50):F2} " +
            $"p75 {Harness.Percentile(qs, 0.75):F2} p90 {Harness.Percentile(qs, 0.90):F2}; " +
            $"within-player IQR of the percentile, median {Harness.Percentile(consistency.OrderBy(x => x).ToArray(), 0.5):F2} " +
            $"(0 = every chart sits at the same place among the peers, 0.5 = anywhere)");

        var held = eval.Select(e => e.Pair).ToList();
        int At(Pair p, double q) => PeerEstimator.Estimate(p.Voices!.Select(v => new PeerScore(v, 0, 0)).ToArray(), 0, q)!.Value;
        var personal = eval.ToDictionary(e => (e.Pair.Player, e.Pair.ChartType, e.Pair.ChartId), e => At(e.Pair, e.Q));
        var shrunk = eval.ToDictionary(e => (e.Pair.Player, e.Pair.ChartType, e.Pair.ChartId), e => At(e.Pair, e.Shrunk));
        Row("held-out half · p50", held, p => p.Estimate, mix);
        Row("held-out half · p25", held, p => p.Q1!.Value, mix);
        Row("held-out half · personal q", held, p => personal[(p.Player, p.ChartType, p.ChartId)], mix);
        Row("held-out half · half-shrunk q", held, p => shrunk[(p.Player, p.ChartType, p.ChartId)], mix);
    }

    // ------------------------------------------------------------------ the Phoenix 1 sample

    /// <summary>
    ///     The same shape on Phoenix 1, over a deterministic sample of players (every k-th account
    ///     with Phoenix stats, ordered by id; <c>SCORETRACKER_PROBE_SAMPLE</c> sets the target count,
    ///     default 60) because a Phoenix 1 band is hundreds of players and every one of the 1,500
    ///     accounts would take hours. Targets are the page's own window — the player's charts within
    ///     two levels of their competitive level — so the peer read stays the size the page pays for.
    ///     Truth is the player's current best, which on Phoenix 1 is the eventual best the shipping
    ///     p65 was fitted on; the quartile rows say what a lower or higher read would have claimed
    ///     against that same truth.
    /// </summary>
    [CatalogProbeFact]
    public async Task Phoenix1_sampled_backtest_against_actual_scores()
    {
        await using var services = BuildServices();
        var projector = services.GetRequiredService<IScoreProjector>();
        var statsRepository = services.GetRequiredService<IPlayerStatsRepository>();
        var stats = services.GetRequiredService<IPlayerStatsReader>();
        var scores = services.GetRequiredService<IScoreReader>();
        var chartRepository = services.GetRequiredService<IChartRepository>();

        var charts = (await chartRepository.GetCharts(MixEnum.Phoenix, cancellationToken: CancellationToken.None))
            .ToDictionary(c => c.Id);
        var everyone = (await statsRepository.GetUserIdsWithStats(MixEnum.Phoenix, CancellationToken.None))
            .OrderBy(id => id).ToArray();
        var wanted = int.TryParse(Environment.GetEnvironmentVariable("SCORETRACKER_PROBE_SAMPLE"), out var n) ? n : 60;
        var step = Math.Max(1, everyone.Length / Math.Max(1, wanted));
        var players = everyone.Where((_, i) => i % step == 0).ToArray();

        var pairs = new List<Pair>();
        var universe = 0;
        var sampled = 0;
        foreach (var player in players)
        {
            var playerStats = await stats.GetStats(MixEnum.Phoenix, player, CancellationToken.None);
            var mine = (await scores.GetBestScores(MixEnum.Phoenix, player, CancellationToken.None))
                .Where(s => s is { Score: not null, IsBroken: false } && charts.ContainsKey(s.ChartId))
                .ToArray();
            var counted = false;
            foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
            {
                var level = chartType == ChartType.Single
                    ? playerStats.SinglesCompetitiveLevel
                    : playerStats.DoublesCompetitiveLevel;
                if (level < MinimumLevelForBacktest) continue;

                var held = mine.Where(s => charts[s.ChartId].Type == chartType && (int)charts[s.ChartId].Level >= 10
                                           && Math.Abs((int)charts[s.ChartId].Level - level) <= 2)
                    .ToArray();
                if (held.Length == 0) continue;
                counted = true;
                universe += held.Length;

                var projection = await projector.Project(new ScoreProjectionRequest(MixEnum.Phoenix, chartType,
                    player, held.Select(s => new ProjectionTarget(s.ChartId, (int)charts[s.ChartId].Level)).ToArray(),
                    PeerEstimator.CompetitiveWindow), CancellationToken.None);

                foreach (var s in held)
                    if (projection.Scores.TryGetValue(s.ChartId, out var estimate))
                        pairs.Add(Pair.Of(player, chartType, s.ChartId, (int)estimate, (int)s.Score!.Value, false,
                            projection.Spreads));
            }

            if (counted) sampled++;
        }

        _output.WriteLine($"players sampled: {players.Length} of {everyone.Length} (every {step}th); with a level >= {MinimumLevelForBacktest} type: {sampled}");
        _output.WriteLine($"records held in the ±2 window: {universe}; answered: {pairs.Count} ({100.0 * pairs.Count / Math.Max(1, universe):F1}%)");
        Report("all", pairs, MixEnum.Phoenix);
        Report("singles", pairs.Where(p => p.ChartType == ChartType.Single).ToList(), MixEnum.Phoenix);
        Report("doubles", pairs.Where(p => p.ChartType == ChartType.Double).ToList(), MixEnum.Phoenix);
        TopOfList(pairs, charts, MixEnum.Phoenix);

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

        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        foreach (var pool in new ChartType?[] { null, ChartType.Single, ChartType.Double })
        {
            var projection = await mediator.Send(new ProjectPumbilityGainsQuery(userId, MixEnum.Phoenix2, pool),
                CancellationToken.None);
            _output.WriteLine($"=== {userId} · pool {pool?.ToString() ?? "All"} ===");
            if (projection.Peers != null)
                foreach (var (type, group) in projection.Peers)
                    _output.WriteLine($"  {type}: {group.Kind}, centre {group.Center}, size {group.Size}, pool {group.PoolCount}/{group.PoolSize}, lit {group.IsLit}");

            // The page's own bar, rebuilt the way PumbilityProjectionSaga.BuildPool does, so a gain
            // re-priced at another quantile is measured against the same number the page uses.
            var top = (await mediator.Send(new GetTop50ForPlayerQuery(userId, pool, 100, MixEnum.Phoenix2),
                    CancellationToken.None))
                .Where(s => s.Score != null && charts.ContainsKey(s.ChartId))
                .ToDictionary(s => s.ChartId,
                    s => scoring.GetScore(charts[s.ChartId], s.Score!.Value, s.Plate ?? PhoenixPlate.RoughGame, s.IsBroken));
            var baseline = top.Count >= 50 ? top.Values.OrderByDescending(v => v).Take(50).Min() : 0;

            double GainAt(Guid chartId, int score)
            {
                var phoenix = PhoenixScore.From(score);
                var value = scoring.GetScore(charts[chartId], phoenix, ScoringConfiguration.ExpectedPlateForScore(phoenix), false);
                var floor = top.TryGetValue(chartId, out var current) ? Math.Max(current, baseline) : baseline;
                return value - floor;
            }

            var paying = new Dictionary<string, int> { ["p25"] = 0, ["p50"] = 0, ["p75"] = 0 };
            var ssCalls = new Dictionary<string, int> { ["p25"] = 0, ["p50"] = 0, ["p75"] = 0 };
            foreach (var (chartId, _) in projection.ProjectedGains)
            {
                var spread = projection.Spreads?.GetValueOrDefault(chartId);
                if (spread == null) continue;
                foreach (var (label, score) in new[]
                         {
                             ("p25", (int)spread.Quartile1), ("p50", (int)projection.ExpectedScores[chartId]),
                             ("p75", (int)spread.Quartile3)
                         })
                {
                    if (GainAt(chartId, score) > 0) paying[label]++;
                    if (score >= 980_000) ssCalls[label]++;
                }
            }

            _output.WriteLine($"  bar {baseline:F2}; listed rows {projection.ProjectedGains.Count} (capped at 100 by the saga); " +
                              $"paying at p25 {paying["p25"]} / p50 {paying["p50"]} / p75 {paying["p75"]}; " +
                              $"SS+ calls at p25 {ssCalls["p25"]} / p50 {ssCalls["p50"]} / p75 {ssCalls["p75"]}");
            _output.WriteLine($"  {"chart",-34} {"lvl",-4} {"p25",-16} {"p50",-16} {"p75",-16} {"gain@25",8} {"gain@50",8} {"gain@75",8} peers");
            var rows = projection.ProjectedGains.OrderByDescending(kv => kv.Value).Take(40);
            foreach (var (chartId, gain) in rows)
            {
                var chart = charts[chartId];
                var score = (int)projection.ExpectedScores[chartId];
                var spread = projection.Spreads?.GetValueOrDefault(chartId);
                string Cell(int s) => $"{s,7} {PhoenixScore.From(s).LetterGradeFor(MixEnum.Phoenix2),-8}";
                string GainCell(int s) => GainAt(chartId, s) is var g && g > 0 ? $"+{g,6:F1}" : $"{"—",7}";
                _output.WriteLine($"  {chart.Song.Name,-34} {chart.Type.ToString()[0]}{(int)chart.Level,-3} " +
                                  $"{(spread == null ? "".PadRight(16) : Cell((int)spread.Quartile1))} {Cell(score)} " +
                                  $"{(spread == null ? "".PadRight(16) : Cell((int)spread.Quartile3))} " +
                                  $"{(spread == null ? "".PadRight(8) : GainCell((int)spread.Quartile1))} +{gain,6:F1} " +
                                  $"{(spread == null ? "".PadRight(8) : GainCell((int)spread.Quartile3))} {spread?.PeerCount}");
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

    /// <summary>
    ///     One block per label: the shipping estimate's row (p50 on Phoenix 2, p65 on Phoenix 1),
    ///     then the same pairs read at the peers' first and third quartiles — what "a good day" and
    ///     "the top of my game" would have claimed against the same truth. Coverage is the share of
    ///     actual scores at or above the estimate: a calibrated p25 should sit near 75%, a p50 near
    ///     50%. "grade ≤" is how often the estimate's letter grade was no higher than the actual one
    ///     — the never-overstated rate.
    /// </summary>
    private void Report(string label, IReadOnlyCollection<Pair> pairs, MixEnum mix)
    {
        if (pairs.Count == 0)
        {
            _output.WriteLine($"{label}: no pairs");
            return;
        }

        var shipping = mix == MixEnum.Phoenix2 ? "p50" : "p65";
        Row($"{label} · {shipping}", pairs, p => p.Estimate, mix);
        var withSpread = pairs.Where(p => p.Q1 != null).ToArray();
        if (withSpread.Length == 0) return;
        Row($"{label} · p25", withSpread, p => p.Q1!.Value, mix);
        Row($"{label} · p75", withSpread, p => p.Q3!.Value, mix);
    }

    private void Row(string label, IReadOnlyCollection<Pair> pairs, Func<Pair, int> estimate, MixEnum mix)
    {
        var diffs = pairs.Select(p => (double)(estimate(p) - p.Actual)).OrderBy(d => d).ToArray();
        var ssCalls = pairs.Where(p => estimate(p) >= 980_000).ToArray();
        var ssRight = ssCalls.Count(p => p.Actual >= 980_000);
        var covered = pairs.Count(p => p.Actual >= estimate(p));
        var gradeExact = pairs.Count(p =>
            PhoenixScore.From(estimate(p)).LetterGradeFor(mix) == PhoenixScore.From(p.Actual).LetterGradeFor(mix));
        var gradeNotOver = pairs.Count(p =>
            PhoenixScore.From(estimate(p)).LetterGradeFor(mix) <= PhoenixScore.From(p.Actual).LetterGradeFor(mix));
        _output.WriteLine(
            $"{label,-34} pairs {pairs.Count,5} | bias mean {diffs.Average(),+7:F0} median {Harness.Percentile(diffs, 0.5),+7:F0} " +
            $"| MAE {diffs.Average(Math.Abs),6:F0} | coverage {100.0 * covered / pairs.Count,5:F1}% " +
            $"| grade = {100.0 * gradeExact / pairs.Count,4:F1}%  grade ≤ {100.0 * gradeNotOver / pairs.Count,4:F1}% " +
            $"| says SS+ {ssCalls.Length,4} ({100.0 * ssCalls.Length / pairs.Count,4:F1}%), right {100.0 * ssRight / Math.Max(1, ssCalls.Length),4:F1}% " +
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

    /// <summary>
    ///     One estimate against one truth, with the peers' quartiles beside it so the same pairs can
    ///     be re-read at p25 and p75 without a second sweep — the quartiles ride out of the projector
    ///     over the same voices and weights as the estimate.
    /// </summary>
    private sealed record Pair(Guid Player, ChartType ChartType, Guid ChartId, int Estimate, int Actual, bool Debut,
        int? Q1, int? Q3, IReadOnlyList<int>? Voices)
    {
        public static Pair Of(Guid player, ChartType chartType, Guid chartId, int estimate, int actual, bool debut,
            IReadOnlyDictionary<Guid, PeerSpread>? spreads, IReadOnlyList<int>? voices = null)
        {
            var spread = spreads?.GetValueOrDefault(chartId);
            return new Pair(player, chartType, chartId, estimate, actual, debut,
                spread == null ? null : (int)spread.Quartile1, spread == null ? null : (int)spread.Quartile3, voices);
        }

        /// <summary>Where the actual score sat among the peers' voices, midpoint rank on 0..1; null without voices.</summary>
        public double? Percentile => Voices is { Count: > 0 } v
            ? (v.Count(x => x < Actual) + 0.5 * v.Count(x => x == Actual)) / v.Count
            : null;
    }

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
