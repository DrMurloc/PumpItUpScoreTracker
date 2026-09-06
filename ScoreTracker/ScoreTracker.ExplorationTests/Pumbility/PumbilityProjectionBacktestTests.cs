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
///     The PUMBILITY projection's measurement harness (docs/design/pumbility-overhaul.md §4.8, §4.10, §9).
///     <para>
///         Probes against a populated database and one pin that needs none:
///     </para>
///     <list type="bullet">
///         <item>
///             <b>The Phoenix 2 backtest.</b> Every Phoenix 2 player, every chart they hold a
///             Phoenix 2 score on, the shipping projector run for them exactly as the page runs it —
///             their own scores never enter their own peer group — read at the peers' 25th, 50th and
///             75th percentile, and the truth is the score they actually hold. Reported as bias, MAE,
///             coverage (the share of actual scores at or above the estimate), the never-overstated
///             rate, how often an SS call was right, and how much of the record was answered at all.
///             Self-selected (players choose what they play), which favours every rung alike.
///         </item>
///         <item>
///             <b>The top of the list.</b> Per player-type, the answered charts ranked by projected
///             PUMBILITY value — the page's own order — and the top ten read against the rest, each
///             rung ranking its own list. Sorting by an estimate selects the charts where it ran high,
///             so this is where a rung's honesty is decided (§4.10, D50).
///         </item>
///         <item>
///             <b>A per-player quantile</b>, split-half: where a player's own scores sit among their
///             peers, fitted on half their charts and scored on the other half against the fixed rungs.
///         </item>
///         <item>
///             <b>A sampled Phoenix 1 backtest</b>, same shape, in the page's own ±2 window.
///         </item>
///         <item>
///             <b>One player's list</b>, with the peers behind each row — the reproduction that found
///             the 2026-08-13 inflation, kept so the next "this feels high" can be answered the same
///             afternoon.
///         </item>
///         <item>
///             <b>The pin.</b> <see cref="PeerEstimator" /> and this file's own arithmetic agree on
///             fixed inputs, so a change to either shows up here rather than as a silent drift
///             between what was measured and what ships. This one always runs.
///         </item>
///     </list>
///     <para>
///         Configure <c>CatalogProbe:ConnectionString</c> (the shared AppHost user-secrets store) or
///         the <c>SCORETRACKER_CATALOG_CONNECTION</c> variable, optionally <c>PumbilityProbe:UserId</c>
///         / <c>SCORETRACKER_PUMBILITY_PROBE_USER</c> for the list and <c>SCORETRACKER_PROBE_SAMPLE</c>
///         for the Phoenix 1 sample size, then
///         <c>dotnet test ScoreTracker/ScoreTracker.ExplorationTests/ScoreTracker.ExplorationTests.csproj --filter "FullyQualifiedName~PumbilityProjection"</c>.
///         Read-only.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class PumbilityProjectionBacktestTests
{
    private const int MinimumLevelForBacktest = 15;

    /// <summary>The three rungs the page offers (D51), read off one sweep.</summary>
    private static readonly double[] Rungs = { PeerEstimator.LowerQuartile, PeerEstimator.Median, PeerEstimator.UpperQuartile };

    private readonly ITestOutputHelper _output;

    public PumbilityProjectionBacktestTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ------------------------------------------------------------------ the pin

    [Fact]
    public void The_estimator_and_the_harness_agree_on_fixed_inputs()
    {
        // Phoenix 2: unweighted, five-peer floor — the median rung and the default rung.
        var five = new[] { 940_000, 985_000, 962_000, 990_000, 975_000 };
        Assert.Equal(Harness.Median(five),
            PeerEstimator.Estimate(five.Select(s => new PeerScore(s, 0, 0)).ToArray(), 0,
                PeerEstimator.Median, PeerEstimator.Phoenix2MinimumPeers));
        var six = five.Append(999_000).ToArray();
        Assert.Equal(Harness.Median(six),
            PeerEstimator.Estimate(six.Select(s => new PeerScore(s, 0, 0)).ToArray(), 0,
                PeerEstimator.Median, PeerEstimator.Phoenix2MinimumPeers));
        Assert.Null(PeerEstimator.Estimate(five.Take(4).Select(s => new PeerScore(s, 0, 0)).ToArray(), 0,
            PeerEstimator.Median, PeerEstimator.Phoenix2MinimumPeers));
        Assert.Equal((int)Math.Round(Harness.Percentile(five.OrderBy(s => s).Select(s => (double)s).ToArray(), PeerEstimator.DefaultQuantile)),
            PeerEstimator.Estimate(five.Select(s => new PeerScore(s, 0, 0)).ToArray(), 0));

        // Phoenix 1: growth-weighted, midpoint convention, at the default rung and on the ladder.
        var peers = new[]
        {
            new PeerScore(900_000, 22.0, 19.0), new PeerScore(950_000, 22.0, 22.0),
            new PeerScore(965_000, 22.0, 21.5), new PeerScore(975_000, 22.0, 22.0),
            new PeerScore(985_000, 22.0, 22.0), new PeerScore(990_000, 22.0, 20.0)
        };
        var weighted = peers.Select(p => ((double)p.Score, Math.Exp(-p.Growth))).ToArray();
        Assert.Equal((int)Math.Round(Harness.WeightedQuantile(weighted, PeerEstimator.DefaultQuantile)),
            PeerEstimator.Estimate(peers));
        var ladder = PeerEstimator.Ladder(peers, Rungs)!;
        foreach (var rung in Rungs)
            Assert.Equal((int)Math.Round(Harness.WeightedQuantile(weighted, rung)), (int)ladder.At(rung));
    }

    // ------------------------------------------------------------------ the Phoenix 2 backtest

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
                // (PeerPools) — the personal-quantile experiment needs where each actual score sat
                // among the peers, not just the rungs.
                var projection = await projector.Project(new ScoreProjectionRequest(MixEnum.Phoenix2, chartType,
                    player, held.Select(s => new ProjectionTarget(s.ChartId, (int)charts[s.ChartId].Level)).ToArray(),
                    PeerEstimator.CompetitiveWindow, charts, Quantiles: Rungs), CancellationToken.None);
                if (projection.Group is { IsLit: true }) lit[chartType]++;

                foreach (var s in held)
                    if (projection.Ladders != null && projection.Ladders.TryGetValue(s.ChartId, out var ladder))
                        pairs.Add(Pair.Of(player, chartType, s.ChartId, ladder, (int)s.Score!.Value,
                            !phoenix1.Contains(s.ChartId),
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

    // ------------------------------------------------------------------ the Phoenix 1 sample

    /// <summary>
    ///     The same shape on Phoenix 1, over a deterministic sample of players (every k-th account
    ///     with Phoenix stats, ordered by id; <c>SCORETRACKER_PROBE_SAMPLE</c> sets the target count,
    ///     default 60) because a Phoenix 1 band is hundreds of players and every one of the accounts
    ///     would take hours. Targets are the page's own window — the player's charts within two
    ///     levels of their competitive level — so the peer read stays the size the page pays for.
    ///     Truth is the player's current best.
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
                    PeerEstimator.CompetitiveWindow, Quantiles: Rungs), CancellationToken.None);

                foreach (var s in held)
                    if (projection.Ladders != null && projection.Ladders.TryGetValue(s.ChartId, out var ladder))
                        pairs.Add(Pair.Of(player, chartType, s.ChartId, ladder, (int)s.Score!.Value, false, null));
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
            // The page's three energies off the one cached sweep (D51): Good is the list the page
            // opens on; Great and Top of my game re-read the same rows.
            var projection = await mediator.Send(new ProjectPumbilityGainsQuery(userId, MixEnum.Phoenix2, pool),
                CancellationToken.None);
            var great = await mediator.Send(new ProjectPumbilityGainsQuery(userId, MixEnum.Phoenix2, pool, Energy.Great),
                CancellationToken.None);
            var best = await mediator.Send(new ProjectPumbilityGainsQuery(userId, MixEnum.Phoenix2, pool, Energy.TopOfMyGame),
                CancellationToken.None);
            _output.WriteLine($"=== {userId} · pool {pool?.ToString() ?? "All"} ===");
            if (projection.Peers != null)
                foreach (var (type, group) in projection.Peers)
                    _output.WriteLine($"  {type}: {group.Kind}, centre {group.Center}, size {group.Size}, pool {group.PoolCount}/{group.PoolSize}, lit {group.IsLit}");

            // The page's own bar, rebuilt the way PumbilityProjectionSaga.BuildPool does, so a gain
            // re-priced at another rung is measured against the same number the page uses.
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

            _output.WriteLine($"  bar {baseline:F2}; listed rows Good {projection.ProjectedGains.Count} / Great {great.ProjectedGains.Count} / Top {best.ProjectedGains.Count} (each capped at 100 by the saga); " +
                              $"SS+ calls Good {projection.ExpectedScores.Values.Count(v => (int)v >= 980_000)} / Great {great.ExpectedScores.Values.Count(v => (int)v >= 980_000)} / Top {best.ExpectedScores.Values.Count(v => (int)v >= 980_000)}");
            _output.WriteLine($"  {"chart",-34} {"lvl",-4} {"Good",-16} {"Great",-16} {"Top of my game",-16} {"gain",8} {"gain",8} {"gain",8}");
            var rows = projection.ProjectedGains.OrderByDescending(kv => kv.Value).Take(40);
            foreach (var (chartId, gain) in rows)
            {
                var chart = charts[chartId];
                string Cell(PumbilityProjection p) => p.ExpectedScores.TryGetValue(chartId, out var v)
                    ? $"{(int)v,7} {v.LetterGradeFor(MixEnum.Phoenix2),-8}"
                    : "".PadRight(16);
                string GainCell(PumbilityProjection p) => p.ExpectedScores.TryGetValue(chartId, out var v) && GainAt(chartId, (int)v) is var g && g > 0
                    ? $"+{g,6:F1}"
                    : $"{"—",7}";
                _output.WriteLine($"  {chart.Song.Name,-34} {chart.Type.ToString()[0]}{(int)chart.Level,-3} " +
                                  $"{Cell(projection)} {Cell(great)} {Cell(best)} {GainCell(projection)} {GainCell(great)} {GainCell(best)}");
            }
        }

        Assert.True(true, "a reproduction, not a guarantee — read the output");
    }

    // ------------------------------------------------------------------ who the scorers are, by level

    /// <summary>
    ///     The owner's question of 2026-09-01: "there's some charts that only people ABOVE my level
    ///     have … what's the level distribution look like on that group?" For the probe user, every
    ///     chart on their list whose song matches <c>SCORETRACKER_PROBE_CHART</c> (default "King"):
    ///     each PUMBILITY peer who scored it, with their rung, total and competitive level beside the
    ///     viewer's, and the three rungs the page would read.
    /// </summary>
    [CatalogProbeFact]
    public async Task One_charts_scorers_by_level()
    {
        await using var services = BuildServices();
        var projector = services.GetRequiredService<IScoreProjector>();
        var statsRepository = services.GetRequiredService<IPlayerStatsRepository>();
        var stats = services.GetRequiredService<IPlayerStatsReader>();
        var scores = services.GetRequiredService<IScoreReader>();
        var chartRepository = services.GetRequiredService<IChartRepository>();

        var userId = ProbeUserId ??
                     (await statsRepository.GetUserIdsWithStats(MixEnum.Phoenix2, CancellationToken.None)).First();
        var filter = Environment.GetEnvironmentVariable("SCORETRACKER_PROBE_CHART") ?? "King";
        var charts = (await chartRepository.GetCharts(MixEnum.Phoenix2, cancellationToken: CancellationToken.None))
            .ToDictionary(c => c.Id);
        var wanted = charts.Values.Where(c => c.Song.Name.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)
                                              && c.Type is ChartType.Single or ChartType.Double).ToArray();
        var mine = await stats.GetStats(MixEnum.Phoenix2, userId, CancellationToken.None);
        var myRung = Phoenix2PumbilityLevel.From(mine.SkillRating);
        _output.WriteLine($"viewer {userId}: total {mine.SkillRating:F0} → rung {Rung(myRung)}; competitive S {mine.SinglesCompetitiveLevel:F2} / D {mine.DoublesCompetitiveLevel:F2}");

        foreach (var type in new[] { ChartType.Single, ChartType.Double })
        {
            var targets = wanted.Where(c => c.Type == type).ToArray();
            if (targets.Length == 0) continue;
            var projection = await projector.Project(new ScoreProjectionRequest(MixEnum.Phoenix2, type, userId,
                targets.Select(c => new ProjectionTarget(c.Id, (int)c.Level)).ToArray(), PeerEstimator.CompetitiveWindow,
                charts, Quantiles: Rungs), CancellationToken.None);
            if (projection.PeerPools == null)
            {
                _output.WriteLine($"{type}: dark — {projection.Group?.PoolCount}/{projection.Group?.PoolSize} charts");
                continue;
            }

            var peers = projection.PeerPools.Peers.Where(v => v.UserId != null)
                .Select(v => v.UserId!.Value).ToArray();
            var peerStats = (await stats.GetStats(MixEnum.Phoenix2, peers, CancellationToken.None)).ToDictionary(p => p.UserId);
            var voices = (await scores.GetPlayerScores(MixEnum.Phoenix2, peers, targets.Select(c => c.Id), CancellationToken.None))
                .Where(v => !v.IsBroken).GroupBy(v => v.ChartId).ToDictionary(g => g.Key, g => g.ToArray());
            _output.WriteLine($"=== {type}: {peers.Length} PUMBILITY peers, pools {projection.Group?.Lowest:F0}..{projection.Group?.Highest:F0} around your {type} pool {projection.Group?.Center:F0} (D53) ===");
            var bandRungs = peers.Select(p => Phoenix2PumbilityLevel.From(peerStats[p].SkillRating).Index - myRung.Index)
                .GroupBy(o => o).OrderBy(g => g.Key).Select(g => $"{g.Key:+0;-0;0}:{g.Count()}");
            _output.WriteLine($"  the band by rung offset from you (offset:count): {string.Join("  ", bandRungs)}");

            foreach (var chart in targets.OrderBy(c => (int)c.Level))
            {
                _output.WriteLine($"--- {chart.Song.Name} {chart.Type.ToString()[0]}{(int)chart.Level} ---");
                if (!voices.TryGetValue(chart.Id, out var rows) || rows.Length == 0)
                {
                    _output.WriteLine("  no peer has scored it");
                    continue;
                }

                var ladder = projection.Ladders?.GetValueOrDefault(chart.Id);
                _output.WriteLine(ladder == null
                    ? $"  {rows.Length} scorer(s), under the five-peer floor: no projection"
                    : $"  projection at Good {Grade(ladder.At(0.25))} / Great {Grade(ladder.At(0.5))} / Top {Grade(ladder.At(0.75))} from {ladder.PeerCount} peers");
                var offsets = new List<int>();
                foreach (var v in rows.OrderByDescending(v => (int)v.Score))
                {
                    var st = peerStats[v.UserId];
                    var rung = Phoenix2PumbilityLevel.From(st.SkillRating);
                    var offset = rung.Index - myRung.Index;
                    offsets.Add(offset);
                    var level = type == ChartType.Single ? st.SinglesCompetitiveLevel : st.DoublesCompetitiveLevel;
                    var myLevel = type == ChartType.Single ? mine.SinglesCompetitiveLevel : mine.DoublesCompetitiveLevel;
                    _output.WriteLine($"  {(v.IsPublic ? v.UserName.ToString() : "(private)"),-22} {Grade(v.Score),-18} total {st.SkillRating,9:F0}  rung {Rung(rung),-22} {offset,+3:+0;-0;0} vs you   comp {level,5:F2} ({level - myLevel:+0.00;-0.00})");
                }

                offsets.Sort();
                _output.WriteLine($"  scorers vs your rung: {offsets.Count(o => o < 0)} below · {offsets.Count(o => o == 0)} level · {offsets.Count(o => o > 0)} above; median offset {Harness.Percentile(offsets.Select(o => (double)o).ToArray(), 0.5):+0.0;-0.0;0}");
            }
        }

        Assert.True(true, "a reproduction, not a guarantee — read the output");
    }

    /// <summary>
    ///     The population version of the same question. For every Phoenix 2 pair the backtest scores,
    ///     the rung of each peer who voted, relative to the viewer's — then the pairs bucketed by the
    ///     scorers' median offset and by the share of scorers above the viewer, each bucket read at
    ///     the three rungs. If a chart that only the upper half of a band plays projects high, the
    ///     bias climbs with the bucket. Two candidate rules are measured on the same pairs so the
    ///     answer comes with its remedy priced: <b>nearby first</b> (the voices within one rung of the
    ///     viewer when five or more, else the whole band) and <b>rung-weighted</b> (every voice at
    ///     exp(−|offset|), the growth weighting's shape on distance instead of time).
    /// </summary>
    [CatalogProbeFact]
    public async Task Phoenix2_scorers_level_offset_against_the_bias()
    {
        await using var services = BuildServices();
        var projector = services.GetRequiredService<IScoreProjector>();
        var statsRepository = services.GetRequiredService<IPlayerStatsRepository>();
        var stats = services.GetRequiredService<IPlayerStatsReader>();
        var scores = services.GetRequiredService<IScoreReader>();
        var chartRepository = services.GetRequiredService<IChartRepository>();

        var charts = (await chartRepository.GetCharts(MixEnum.Phoenix2, cancellationToken: CancellationToken.None))
            .ToDictionary(c => c.Id);
        var players = (await statsRepository.GetUserIdsWithStats(MixEnum.Phoenix2, CancellationToken.None)).ToArray();

        var pairs = new List<Pair>();
        var voicesByPair = new Dictionary<(Guid, ChartType, Guid), (int Score, int Offset)[]>();
        var viewerRung = new Dictionary<(Guid, ChartType), int>();
        foreach (var player in players)
        {
            var mine = (await scores.GetBestScores(MixEnum.Phoenix2, player, CancellationToken.None))
                .Where(s => s is { Score: not null, IsBroken: false } && charts.ContainsKey(s.ChartId))
                .ToArray();
            var playerStats = await stats.GetStats(MixEnum.Phoenix2, player, CancellationToken.None);
            var myRung = Phoenix2PumbilityLevel.From(playerStats.SkillRating).Index;
            foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
            {
                var level = chartType == ChartType.Single ? playerStats.SinglesCompetitiveLevel : playerStats.DoublesCompetitiveLevel;
                if (level < MinimumLevelForBacktest) continue;
                var held = mine.Where(s => charts[s.ChartId].Type == chartType && (int)charts[s.ChartId].Level >= 10).ToArray();
                if (held.Length == 0) continue;
                viewerRung[(player, chartType)] = myRung;

                var projection = await projector.Project(new ScoreProjectionRequest(MixEnum.Phoenix2, chartType,
                    player, held.Select(s => new ProjectionTarget(s.ChartId, (int)charts[s.ChartId].Level)).ToArray(),
                    PeerEstimator.CompetitiveWindow, charts, Quantiles: Rungs), CancellationToken.None);
                if (projection.PeerPools == null || projection.Ladders == null || projection.Ladders.Count == 0) continue;

                var peers = projection.PeerPools.Peers.Where(v => v.UserId != null)
                .Select(v => v.UserId!.Value).ToArray();
                var peerRung = (await stats.GetStats(MixEnum.Phoenix2, peers, CancellationToken.None))
                    .ToDictionary(p => p.UserId, p => Phoenix2PumbilityLevel.From(p.SkillRating).Index - myRung);
                var voices = (await scores.GetPlayerScores(MixEnum.Phoenix2, peers,
                        held.Where(s => projection.Ladders.ContainsKey(s.ChartId)).Select(s => s.ChartId), CancellationToken.None))
                    .Where(v => !v.IsBroken && peerRung.ContainsKey(v.UserId))
                    .GroupBy(v => v.ChartId)
                    .ToDictionary(g => g.Key, g => g.Select(v => ((int)v.Score, peerRung[v.UserId])).ToArray());

                foreach (var s in held)
                    if (projection.Ladders.TryGetValue(s.ChartId, out var ladder) && voices.TryGetValue(s.ChartId, out var v))
                    {
                        pairs.Add(Pair.Of(player, chartType, s.ChartId, ladder, (int)s.Score!.Value, false, null));
                        voicesByPair[(player, chartType, s.ChartId)] = v;
                    }
            }
        }

        _output.WriteLine($"pairs with their scorers' rungs: {pairs.Count}");
        double MedianOffset(Pair p) => Harness.Percentile(voicesByPair[(p.Player, p.ChartType, p.ChartId)].Select(v => (double)v.Offset).OrderBy(x => x).ToArray(), 0.5);
        double ShareAbove(Pair p) { var v = voicesByPair[(p.Player, p.ChartType, p.ChartId)]; return v.Count(x => x.Offset > 0) / (double)v.Length; }

        _output.WriteLine("--- by the scorers' median rung offset from the viewer (shipping ladder) ---");
        foreach (var (label, low, high) in new[] { ("median offset <= -2", double.NegativeInfinity, -1.5), ("median offset -1", -1.5, -0.5), ("median offset 0", -0.5, 0.5), ("median offset +1", 0.5, 1.5), ("median offset >= +2", 1.5, double.PositiveInfinity) })
        {
            var bucket = pairs.Where(p => MedianOffset(p) >= low && MedianOffset(p) < high).ToList();
            if (bucket.Count > 0) Report(label, bucket, MixEnum.Phoenix2);
        }

        _output.WriteLine("--- by the share of scorers above the viewer's rung (shipping ladder) ---");
        foreach (var (label, low, high) in new[] { ("0-25% above", 0.0, 0.25), ("25-50% above", 0.25, 0.5), ("50-75% above", 0.5, 0.75), ("75-100% above", 0.75, 1.01) })
        {
            var bucket = pairs.Where(p => ShareAbove(p) >= low && ShareAbove(p) < high).ToList();
            if (bucket.Count > 0) Report(label, bucket, MixEnum.Phoenix2);
        }

        // Two candidate rules, re-read off the same voices.
        Pair Reprice(Pair p, Func<(int Score, int Offset)[], (int Score, double Weight)[]> weigh)
        {
            var weighted = weigh(voicesByPair[(p.Player, p.ChartType, p.ChartId)])
                .Where(w => w.Weight > 0).Select(w => ((double)w.Score, w.Weight)).ToArray();
            if (weighted.Length == 0) return p;
            int At(double q) => (int)Math.Round(Harness.WeightedQuantile(weighted, q));
            return p with { P25 = At(0.25), P50 = At(0.5), P75 = At(0.75) };
        }

        var nearby = pairs.Select(p => Reprice(p, v =>
        {
            var near = v.Where(x => Math.Abs(x.Offset) <= 1).ToArray();
            var use = near.Length >= PeerEstimator.Phoenix2MinimumPeers ? near : v;
            return use.Select(x => (x.Score, 1.0)).ToArray();
        })).ToList();
        var weighted = pairs.Select(p => Reprice(p, v => v.Select(x => (x.Score, Math.Exp(-Math.Abs(x.Offset)))).ToArray())).ToList();

        _output.WriteLine("--- shipping: the whole band, every voice equal ---");
        Report("band", pairs, MixEnum.Phoenix2);
        TopOfList(pairs, charts, MixEnum.Phoenix2);
        _output.WriteLine("--- candidate: nearby first (within one rung when five or more, else the band) ---");
        Report("nearby", nearby, MixEnum.Phoenix2);
        TopOfList(nearby, charts, MixEnum.Phoenix2);
        _output.WriteLine("--- candidate: rung-weighted, exp(-|offset|) ---");
        Report("rung-weighted", weighted, MixEnum.Phoenix2);
        TopOfList(weighted, charts, MixEnum.Phoenix2);

        // ---- Is the skew everyone's or the top of the ladder's? The same pairs grouped by the
        // viewer's own rung: who scores the charts they hold, and what the top ten reads.
        _output.WriteLine("--- by the viewer's rung (shipping band, every voice equal) ---");
        foreach (var (label, low, high) in new[]
                 {
                     ("viewer rungs 1-10", 1, 10), ("viewer rungs 11-15", 11, 15), ("viewer rungs 16-20", 16, 20),
                     ("viewer rungs 21-25", 21, 25), ("viewer rungs 26-30", 26, 30), ("viewer rungs 31-36", 31, 36)
                 })
        {
            var mine = pairs.Where(p => viewerRung[(p.Player, p.ChartType)] >= low && viewerRung[(p.Player, p.ChartType)] <= high).ToList();
            if (mine.Count == 0) continue;
            var heard = mine.SelectMany(p => voicesByPair[(p.Player, p.ChartType, p.ChartId)]).ToArray();
            var playerTypes = mine.Select(p => (p.Player, p.ChartType)).Distinct().Count();
            _output.WriteLine($"{label}: {playerTypes} player-types, {mine.Count} pairs; of every voice heard " +
                              $"{100.0 * heard.Count(v => v.Offset < 0) / heard.Length:F0}% below / {100.0 * heard.Count(v => v.Offset == 0) / heard.Length:F0}% level / " +
                              $"{100.0 * heard.Count(v => v.Offset > 0) / heard.Length:F0}% above the viewer; " +
                              $"pairs whose scorers' median sits above the viewer: {100.0 * mine.Count(p => MedianOffset(p) > 0.5) / mine.Count:F0}%, below: {100.0 * mine.Count(p => MedianOffset(p) < -0.5) / mine.Count:F0}%");
            foreach (var (rung, at) in Reads.Take(2))
            {
                Row($"  {label} · all · {rung}", mine, at, MixEnum.Phoenix2);
                var top = Top(mine, charts, at, MixEnum.Phoenix2);
                if (top.Count > 0) Row($"  {label} · top 10 · {rung}", top, at, MixEnum.Phoenix2);
            }
        }

        // ---- Asymmetric peer windows, re-read off the same voices. Strict: under five voices in
        // the window the chart is not shown. Fallback: the whole band answers instead, as D47 does.
        _output.WriteLine("--- peer windows in rungs around the viewer, re-read off the same voices ---");
        int Quantile((double, double)[] voicesAt, double q) => (int)Math.Round(Harness.WeightedQuantile(voicesAt, q));
        foreach (var (label, low, high) in new[]
                 {
                     ("-3..+3 (ships)", -3, 3), ("-2..+1", -2, 1), ("-3..+1", -3, 1), ("-3..+2", -3, 2), ("-2..+2", -2, 2),
                     ("-1..+1", -1, 1), ("-3..0", -3, 0)
                 })
        {
            var strict = new List<Pair>();
            var fallback = new List<Pair>();
            foreach (var p in pairs)
            {
                var inside = voicesByPair[(p.Player, p.ChartType, p.ChartId)]
                    .Where(x => x.Offset >= low && x.Offset <= high).Select(x => ((double)x.Score, 1.0)).ToArray();
                if (inside.Length >= PeerEstimator.Phoenix2MinimumPeers)
                {
                    var re = p with { P25 = Quantile(inside, 0.25), P50 = Quantile(inside, 0.5), P75 = Quantile(inside, 0.75) };
                    strict.Add(re);
                    fallback.Add(re);
                }
                else
                {
                    fallback.Add(p);
                }
            }

            _output.WriteLine($"[{label}] strict answers {100.0 * strict.Count / pairs.Count:F0}% of the pairs the band answers");
            foreach (var (rung, at) in Reads.Take(2))
            {
                Row($"  {label} strict · all · {rung}", strict, at, MixEnum.Phoenix2);
                var topStrict = Top(strict, charts, at, MixEnum.Phoenix2);
                if (topStrict.Count > 0) Row($"  {label} strict · top 10 · {rung}", topStrict, at, MixEnum.Phoenix2);
                var topFallback = Top(fallback, charts, at, MixEnum.Phoenix2);
                if (topFallback.Count > 0) Row($"  {label} fallback · top 10 · {rung}", topFallback, at, MixEnum.Phoenix2);
            }
        }

        Assert.True(true, "a measurement, not a guarantee — read the output");
    }

    /// <summary>The top ten of each player-type's list at one read, for lists of twenty or more (the TopOfList rule).</summary>
    private static List<Pair> Top(IReadOnlyCollection<Pair> pairs, IReadOnlyDictionary<Guid, Chart> charts, Func<Pair, int> at, MixEnum mix)
    {
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);
        double Value(Pair p)
        {
            var phoenix = PhoenixScore.From(at(p));
            return scoring.GetScore(charts[p.ChartId], phoenix, ScoringConfiguration.ExpectedPlateForScore(phoenix), false);
        }

        var top = new List<Pair>();
        foreach (var group in pairs.GroupBy(p => (p.Player, p.ChartType)))
        {
            var ordered = group.OrderByDescending(Value).ToArray();
            if (ordered.Length < 20) continue;
            top.AddRange(ordered.Take(10));
        }

        return top;
    }

    private static string Rung(Phoenix2PumbilityLevel rung) =>
        rung.Gem is { } gem ? $"{gem} LV.{rung.Level} (#{rung.Index})" : $"unranked (#{rung.Index})";

    private static string Grade(PhoenixScore score) => $"{(int)score:N0} {score.LetterGradeFor(MixEnum.Phoenix2)}";

    // ------------------------------------------------------------------ per-type pool windows

    /// <summary>
    ///     The owner's proposal of 2026-09-01: draw peers not from the combined total's rung band but
    ///     from the viewer's pool OF THE TYPE — players whose singles pool sits within ±X PUMBILITY of
    ///     the viewer's singles pool for a singles chart, doubles for doubles — with the same full-pool
    ///     gate on both sides and the same five-voice floor. The combined total is type-blind: a
    ///     singles-carried player's doubles peers are doubles specialists. Measured at ±250, ±500 and
    ///     ±1000 against the shipping band on the same charts, and for the probe user the group sizes
    ///     and one chart's roster under the new rule (<c>SCORETRACKER_PROBE_CHART</c>).
    /// </summary>
    [CatalogProbeFact]
    public async Task Phoenix2_pertype_pool_window_against_the_rung_band()
    {
        await using var services = BuildServices();
        var projector = services.GetRequiredService<IScoreProjector>();
        var statsRepository = services.GetRequiredService<IPlayerStatsRepository>();
        var stats = services.GetRequiredService<IPlayerStatsReader>();
        var scores = services.GetRequiredService<IScoreReader>();
        var chartRepository = services.GetRequiredService<IChartRepository>();

        var charts = (await chartRepository.GetCharts(MixEnum.Phoenix2, cancellationToken: CancellationToken.None))
            .ToDictionary(c => c.Id);
        var players = (await statsRepository.GetUserIdsWithStats(MixEnum.Phoenix2, CancellationToken.None)).ToArray();
        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        // Symmetric windows, and asymmetric ones that cut the top the way the rung-window probe found
        // pays: PUMBILITY below the viewer's pool of the type, and above it.
        var windows = new (string Label, double Below, double Above)[]
        {
            ("±250", 250, 250), ("±500", 500, 500), ("±1000", 1000, 1000),
            ("-500..+250", 500, 250), ("-750..+250", 750, 250), ("-500..0", 500, 0), ("-750..+500", 750, 500)
        };
        var probeUser = ProbeUserId;
        var filter = Environment.GetEnvironmentVariable("SCORETRACKER_PROBE_CHART") ?? "Tomb";

        var shipping = new List<Pair>();
        var byWindow = windows.ToDictionary(w => w.Label, _ => new List<Pair>());
        var groupSizes = windows.ToDictionary(w => w.Label, _ => new List<int>());
        var shippingSizes = new List<int>();

        foreach (var type in new[] { ChartType.Single, ChartType.Double })
        {
            // Everyone's records of the type, once: each player's pool of the type is their fifty
            // highest-priced non-broken records, exactly the rule the tier lists' writer applies.
            var records = (await scores.GetPlayerScoresInLevelRange(MixEnum.Phoenix2, players, type,
                    PeerGroup.PumbilityPoolFloor, DifficultyLevel.Max, CancellationToken.None))
                .Where(r => !r.IsBroken && charts.ContainsKey(r.ChartId)).ToArray();
            var byPlayer = records.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.ToArray());
            var poolTotal = new Dictionary<Guid, double>();
            foreach (var (player, mine) in byPlayer)
            {
                var priced = mine.Select(r => scoring.GetScore(charts[r.ChartId], r.Score, r.Plate ?? PhoenixPlate.RoughGame, false))
                    .Where(v => v > 0).OrderByDescending(v => v).ToArray();
                if (priced.Length >= PeerGroup.PumbilityPoolSize) poolTotal[player] = priced.Take(PeerGroup.PumbilityPoolSize).Sum();
            }

            var voicesByChart = records.Where(r => poolTotal.ContainsKey(r.UserId))
                .GroupBy(r => r.ChartId).ToDictionary(g => g.Key, g => g.Select(r => (r.UserId, Score: (int)r.Score)).ToArray());

            _output.WriteLine($"=== {type}: {byPlayer.Count} players with records, {poolTotal.Count} with a full pool of the type ===");

            foreach (var viewer in players)
            {
                if (!poolTotal.TryGetValue(viewer, out var myPool)) continue;
                var playerStats = await stats.GetStats(MixEnum.Phoenix2, viewer, CancellationToken.None);
                var level = type == ChartType.Single ? playerStats.SinglesCompetitiveLevel : playerStats.DoublesCompetitiveLevel;
                if (level < MinimumLevelForBacktest) continue;
                if (!byPlayer.TryGetValue(viewer, out var held) || held.Length == 0) continue;

                // The shipping read, exactly as the page runs it.
                var projection = await projector.Project(new ScoreProjectionRequest(MixEnum.Phoenix2, type, viewer,
                    held.Select(r => new ProjectionTarget(r.ChartId, (int)charts[r.ChartId].Level)).ToArray(),
                    PeerEstimator.CompetitiveWindow, Quantiles: Rungs), CancellationToken.None);
                if (projection.Group is { IsLit: true } g) shippingSizes.Add(g.Size);
                if (projection.Ladders != null)
                    foreach (var r in held)
                        if (projection.Ladders.TryGetValue(r.ChartId, out var ladder))
                            shipping.Add(Pair.Of(viewer, type, r.ChartId, ladder, (int)r.Score, false, null));

                // The per-type pool windows, the viewer out.
                foreach (var window in windows)
                {
                    var peers = poolTotal.Where(kv => kv.Key != viewer && kv.Value - myPool >= -window.Below && kv.Value - myPool <= window.Above)
                        .Select(kv => kv.Key).ToHashSet();
                    groupSizes[window.Label].Add(peers.Count);
                    if (viewer == probeUser)
                        _output.WriteLine($"  probe user {type} pool {myPool:F0}: {peers.Count} peers within {window.Label} (shipping band: {projection.Group?.Size})");
                    foreach (var r in held)
                    {
                        if (!voicesByChart.TryGetValue(r.ChartId, out var all)) continue;
                        var voices = all.Where(v => peers.Contains(v.UserId)).Select(v => ((double)v.Score, 1.0)).ToArray();
                        if (voices.Length < PeerEstimator.Phoenix2MinimumPeers) continue;
                        byWindow[window.Label].Add(new Pair(viewer, type, r.ChartId,
                            (int)Math.Round(Harness.WeightedQuantile(voices, 0.25)),
                            (int)Math.Round(Harness.WeightedQuantile(voices, 0.5)),
                            (int)Math.Round(Harness.WeightedQuantile(voices, 0.75)), (int)r.Score, false, null));
                    }
                }
            }

            // The probe user's roster on the filtered song under ±500 of their pool of the type — played
            // or not, since the voices are the point — with each voice's pool of the type beside theirs.
            if (probeUser is { } me && poolTotal.TryGetValue(me, out var myTypePool))
            {
                var near = poolTotal.Where(kv => kv.Key != me && Math.Abs(kv.Value - myTypePool) <= 500).Select(kv => kv.Key).ToHashSet();
                var nearStats = (await stats.GetStats(MixEnum.Phoenix2, near, CancellationToken.None)).ToDictionary(p => p.UserId);
                foreach (var chart in charts.Values.Where(c => c.Type == type && c.Song.Name.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)).OrderBy(c => (int)c.Level))
                {
                    if (!voicesByChart.TryGetValue(chart.Id, out var all)) continue;
                    var rows = all.Where(v => near.Contains(v.UserId)).OrderByDescending(v => v.Score).ToArray();
                    if (rows.Length == 0) continue;
                    var voices = rows.Select(v => ((double)v.Score, 1.0)).ToArray();
                    var read = rows.Length >= PeerEstimator.Phoenix2MinimumPeers
                        ? $"Good {Grade(PhoenixScore.From((int)Math.Round(Harness.WeightedQuantile(voices, 0.25))))} / Great {Grade(PhoenixScore.From((int)Math.Round(Harness.WeightedQuantile(voices, 0.5))))} / Top {Grade(PhoenixScore.From((int)Math.Round(Harness.WeightedQuantile(voices, 0.75))))}"
                        : "under the five-voice floor";
                    _output.WriteLine($"  --- {chart.Song.Name} {type.ToString()[0]}{(int)chart.Level} under ±500 of your {type} pool ({myTypePool:F0}): {rows.Length} voices; {read} ---");
                    foreach (var v in rows)
                    {
                        var st = nearStats[v.UserId];
                        var compLevel = type == ChartType.Single ? st.SinglesCompetitiveLevel : st.DoublesCompetitiveLevel;
                        _output.WriteLine($"    {Grade(PhoenixScore.From(v.Score)),-18} {type} pool {poolTotal[v.UserId],9:F0} ({poolTotal[v.UserId] - myTypePool:+0;-0;0})  combined {st.SkillRating,9:F0}  comp {compLevel:F2}");
                    }
                }
            }
        }

        _output.WriteLine($"shipping band: median group size {Harness.Percentile(shippingSizes.Select(x => (double)x).OrderBy(x => x).ToArray(), 0.5):F0} (p10 {Harness.Percentile(shippingSizes.Select(x => (double)x).OrderBy(x => x).ToArray(), 0.1):F0}, p90 {Harness.Percentile(shippingSizes.Select(x => (double)x).OrderBy(x => x).ToArray(), 0.9):F0})");
        foreach (var window in windows)
        {
            var sizes = groupSizes[window.Label].Select(x => (double)x).OrderBy(x => x).ToArray();
            _output.WriteLine($"{window.Label} of the type pool: median group size {Harness.Percentile(sizes, 0.5):F0} (p10 {Harness.Percentile(sizes, 0.1):F0}, p90 {Harness.Percentile(sizes, 0.9):F0}); " +
                              $"viewers with under five peers: {groupSizes[window.Label].Count(x => x < PeerEstimator.Phoenix2MinimumPeers)} of {groupSizes[window.Label].Count}");
        }

        _output.WriteLine("--- shipping: ±3 rungs of the combined total ---");
        Report("rung band", shipping, MixEnum.Phoenix2);
        TopOfList(shipping, charts, MixEnum.Phoenix2);
        foreach (var window in windows)
        {
            var pairs = byWindow[window.Label];
            // Head to head on the charts both rules answer.
            var both = pairs.Select(p => (p.Player, p.ChartType, p.ChartId)).ToHashSet();
            var shared = shipping.Where(p => both.Contains((p.Player, p.ChartType, p.ChartId))).ToList();
            _output.WriteLine($"--- {window.Label} of the pool of the type: answers {pairs.Count} pairs ({100.0 * pairs.Count / Math.Max(1, shipping.Count):F0}% of the band's {shipping.Count}); head to head on the {shared.Count} both answer ---");
            Row($"    rung band · p25", shared, p => p.P25, MixEnum.Phoenix2);
            Row($"    {window.Label} type pool · p25", pairs, p => p.P25, MixEnum.Phoenix2);
            Row($"    rung band · p50", shared, p => p.P50, MixEnum.Phoenix2);
            Row($"    {window.Label} type pool · p50", pairs, p => p.P50, MixEnum.Phoenix2);
            var topBand = Top(shared, charts, p => p.P50, MixEnum.Phoenix2);
            var topPool = Top(pairs, charts, p => p.P50, MixEnum.Phoenix2);
            if (topBand.Count > 0) Row($"    rung band · top 10 · p50", topBand, p => p.P50, MixEnum.Phoenix2);
            if (topPool.Count > 0) Row($"    {window.Label} type pool · top 10 · p50", topPool, p => p.P50, MixEnum.Phoenix2);
            var topBand25 = Top(shared, charts, p => p.P25, MixEnum.Phoenix2);
            var topPool25 = Top(pairs, charts, p => p.P25, MixEnum.Phoenix2);
            if (topBand25.Count > 0) Row($"    rung band · top 10 · p25", topBand25, p => p.P25, MixEnum.Phoenix2);
            if (topPool25.Count > 0) Row($"    {window.Label} type pool · top 10 · p25", topPool25, p => p.P25, MixEnum.Phoenix2);
        }

        Assert.True(true, "a measurement, not a guarantee — read the output");
    }

    // ------------------------------------------------------------------ the reads

    /// <summary>
    ///     What a player actually sees is the top of a list sorted by projected gain, and sorting by
    ///     an estimate selects for the charts where that estimate ran high — an estimator that is
    ///     centered over everything still hands you a biased top ten. Per player-type, rank the
    ///     answered charts by their projected PUMBILITY value at a rung (the bar is one number
    ///     within a player, so this is the page's own order) and read the top ten at that same rung,
    ///     against the rest of the list. Each rung ranks its own list, because the page would.
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

        var lists = 0;
        foreach (var (label, at) in Reads)
        {
            var top = new List<Pair>();
            var rest = new List<Pair>();
            foreach (var group in pairs.GroupBy(p => (p.Player, p.ChartType)))
            {
                var ordered = group.OrderByDescending(p => Value(p, at(p))).ToArray();
                if (ordered.Length < minimumList) continue;
                top.AddRange(ordered.Take(listTop));
                rest.AddRange(ordered.Skip(listTop));
            }

            lists = top.Count / listTop;
            if (top.Count == 0) continue;
            Row($"top {listTop} of the list · {label}", top, at, mix);
            Row($"the rest of the list · {label}", rest, at, mix);
        }

        _output.WriteLine($"  (lists: {lists} player-types with >= {minimumList} answered charts; each rung ranks its own list)");
    }

    /// <summary>
    ///     Could the rung be the player's own rather than one number for everyone? Split-half: each
    ///     player-type's answered charts are ordered by id, the odd half fits "where do my scores sit
    ///     among my peers" (the median of the actual score's percentile among the peers' voices on
    ///     each chart), and the even half is scored at that quantile — against the fixed rungs on the
    ///     very same pairs. A shrunk variant halves the distance from the median, the usual hedge for
    ///     a personal estimate fitted on a handful of charts. Measured 7% better than the median and
    ///     declined as a product (D51): the knob answers "how am I playing today", not "where do I
    ///     stand".
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
        foreach (var (label, at) in Reads) Row($"held-out half · {label}", held, at, mix);
        Row("held-out half · personal q", held, p => personal[(p.Player, p.ChartType, p.ChartId)], mix);
        Row("held-out half · half-shrunk q", held, p => shrunk[(p.Player, p.ChartType, p.ChartId)], mix);
    }

    /// <summary>The three rungs as reads, the default first and marked as what ships.</summary>
    private static readonly (string Label, Func<Pair, int> At)[] Reads =
    {
        ("p25 (default)", p => p.P25), ("p50", p => p.P50), ("p75", p => p.P75)
    };

    // ------------------------------------------------------------------ plumbing

    private static Guid? ProbeUserId =>
        Guid.TryParse(Environment.GetEnvironmentVariable("SCORETRACKER_PUMBILITY_PROBE_USER"), out var fromEnv)
            ? fromEnv
            : Guid.TryParse(CatalogProbeConfiguration.Setting("PumbilityProbe:UserId"), out var fromSecrets)
                ? fromSecrets
                : null;

    /// <summary>
    ///     One block per label: the same pairs read at each rung. Coverage is the share of actual
    ///     scores at or above the estimate: a calibrated p25 should sit near 75%, a p50 near 50%.
    ///     "grade ≤" is how often the estimate's letter grade was no higher than the actual one —
    ///     the never-overstated rate.
    /// </summary>
    private void Report(string label, IReadOnlyCollection<Pair> pairs, MixEnum mix)
    {
        if (pairs.Count == 0)
        {
            _output.WriteLine($"{label}: no pairs");
            return;
        }

        foreach (var (rung, at) in Reads) Row($"{label} · {rung}", pairs, at, mix);
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
            $"{label,-40} pairs {pairs.Count,5} | bias mean {diffs.Average(),+7:F0} median {Harness.Percentile(diffs, 0.5),+7:F0} " +
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
    ///     One truth against the three rungs read off the same ladder — the same voices and weights
    ///     the shipping estimate uses — with the voices themselves where the run carried them.
    /// </summary>
    private sealed record Pair(Guid Player, ChartType ChartType, Guid ChartId, int P25, int P50, int P75, int Actual,
        bool Debut, IReadOnlyList<int>? Voices)
    {
        public static Pair Of(Guid player, ChartType chartType, Guid chartId, PeerLadder ladder, int actual, bool debut,
            IReadOnlyList<int>? voices)
        {
            return new Pair(player, chartType, chartId, (int)ladder.At(PeerEstimator.LowerQuartile),
                (int)ladder.At(PeerEstimator.Median), (int)ladder.At(PeerEstimator.UpperQuartile), actual, debut, voices);
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
