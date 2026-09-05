using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MediatR;
using ScoreTracker.Catalog.Wiring;
using ScoreTracker.ChartIntelligence.Wiring;
using ScoreTracker.Domain.Services;
using ScoreTracker.Identity.Wiring;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.ExplorationTests.Catalog;
using ScoreTracker.PlayerProgress.Wiring;
using ScoreTracker.ScoreLedger.Wiring;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.Pumbility;

/// <summary>
///     Workshop probe for a "Your Peers" page: what one player's PUMBILITY peers' pools are made
///     of, cross-folder, weighted by pool position (a chart at #1 in a peer's pool scores 50, #50
///     scores 1 — a Borda count over the band), beside the plain holder count and the raw
///     value sum, plus the peers' score spread on each chart and where the viewer stands.
///     <para>
///         Same peer definition as the projection (docs/design/pumbility-overhaul.md §4.8, D53):
///         pools of the type within 500 below and 250 above the viewer's, full pool of the type
///         both sides, viewer excluded.
///         Configure <c>CatalogProbe:ConnectionString</c> or SCORETRACKER_CATALOG_CONNECTION;
///         SCORETRACKER_PUMBILITY_PROBE_USER picks the player. Read-only.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class PumbilityPeerPoolProbeTests
{
    private const int Top = 40;
    private readonly ITestOutputHelper _output;

    public PumbilityPeerPoolProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [CatalogProbeFact]
    public async Task One_players_peer_pools_cross_folder()
    {
        await using var services = BuildServices();
        var stats = services.GetRequiredService<IPlayerStatsReader>();
        var statsRepository = services.GetRequiredService<IPlayerStatsRepository>();
        var scores = services.GetRequiredService<IScoreReader>();
        var chartRepository = services.GetRequiredService<IChartRepository>();
        const MixEnum mix = MixEnum.Phoenix2;

        var userId = ProbeUserId ??
                     (await statsRepository.GetUserIdsWithStats(mix, CancellationToken.None)).First();
        var charts = (await chartRepository.GetCharts(mix, cancellationToken: CancellationToken.None))
            .ToDictionary(c => c.Id);
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);

        var mine = await stats.GetStats(mix, userId, CancellationToken.None);
        var rung = Phoenix2PumbilityLevel.From(mine.SkillRating);
        _output.WriteLine($"=== {userId} · total {mine.SkillRating:F2} · rung {rung.Index} ({rung.Gem} {rung.Level}) · singles pool {mine.SinglesRating:F2} · doubles pool {mine.DoublesRating:F2} ===");

        // The peers of either type, for the merged-fifty split exported after the loop.
        var unionPeers = new HashSet<Guid>();
        foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
        {
            _output.WriteLine("");
            _output.WriteLine($"----- {chartType} -----");
            // The window on the pool of the type (D53), the viewer out.
            var myPoolTotal = chartType == ChartType.Single ? mine.SinglesRating : mine.DoublesRating;
            var candidates = (await stats.GetPlayersByPoolOfType(mix, chartType,
                    myPoolTotal - PeerGroup.PumbilityWindowBelow, myPoolTotal + PeerGroup.PumbilityWindowAbove,
                    CancellationToken.None))
                .ToHashSet();
            candidates.Remove(userId);
            _output.WriteLine($"window candidates (any pool state): {candidates.Count}");
            var records = (await scores.GetPlayerScoresInLevelRange(mix, candidates.Append(userId), chartType,
                    PeerGroup.PumbilityPoolFloor, DifficultyLevel.Max, CancellationToken.None))
                .Where(s => charts.ContainsKey(s.ChartId))
                .ToArray();

            // Every player's priced records of the type, then their pool: the top fifty above zero,
            // in rank order. Same rule as PumbilityPeers.TopPool, kept ordered here.
            var pools = new Dictionary<Guid, (Guid ChartId, double Rating)[]>();
            var byPlayer = records.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.ToArray());
            foreach (var (player, rows) in byPlayer)
            {
                var priced = rows
                    .Select(r => (r.ChartId, Rating: scoring.GetScore(charts[r.ChartId], r.Score,
                        r.Plate ?? PhoenixPlate.RoughGame, r.IsBroken)))
                    .Where(r => r.Rating > 0)
                    .OrderByDescending(r => r.Rating)
                    .ToArray();
                if (priced.Length >= PeerGroup.PumbilityPoolSize)
                    pools[player] = priced.Take(PeerGroup.PumbilityPoolSize).ToArray();
            }

            var viewerLit = pools.TryGetValue(userId, out var myPool);
            var peers = pools.Keys.Where(p => p != userId).ToHashSet();
            unionPeers.UnionWith(peers);
            _output.WriteLine($"viewer pool: {(viewerLit ? "full" : $"{byPlayer.GetValueOrDefault(userId)?.Length ?? 0} records, dark")}; peers with a full pool: {peers.Count}");
            if (peers.Count == 0) continue;

            // Borda: #1 in a peer's pool = 50 points, #50 = 1. Count: peers holding it at all.
            // Value: the PUMBILITY the chart supplies the band in total.
            var borda = new Dictionary<Guid, int>();
            var count = new Dictionary<Guid, int>();
            var value = new Dictionary<Guid, double>();
            var bestRank = new Dictionary<Guid, int>();
            foreach (var peer in peers)
            {
                var pool = pools[peer];
                for (var i = 0; i < pool.Length; i++)
                {
                    var (chartId, rating) = pool[i];
                    borda[chartId] = borda.GetValueOrDefault(chartId) + (PeerGroup.PumbilityPoolSize - i);
                    count[chartId] = count.GetValueOrDefault(chartId) + 1;
                    value[chartId] = value.GetValueOrDefault(chartId) + rating;
                    bestRank[chartId] = Math.Min(bestRank.GetValueOrDefault(chartId, int.MaxValue), i + 1);
                }
            }

            // Score spread among peers who PLAYED it (any non-broken record), not only pool holders.
            var peerScores = records.Where(r => peers.Contains(r.UserId))
                .GroupBy(r => r.ChartId)
                .ToDictionary(g => g.Key, g => g.Select(r => (int)r.Score).OrderBy(s => s).ToArray());
            var myScores = byPlayer.GetValueOrDefault(userId, Array.Empty<UserPhoenixScore>())
                .ToDictionary(r => r.ChartId, r => (int)r.Score);
            var myRank = myPool == null
                ? new Dictionary<Guid, int>()
                : myPool.Select((c, i) => (c.ChartId, Rank: i + 1)).ToDictionary(c => c.ChartId, c => c.Rank);

            var totalBorda = borda.Values.Sum();
            _output.WriteLine($"distinct charts across peer pools: {borda.Count}; total Borda mass {totalBorda} (= {peers.Count} peers × 1275)");

            _output.WriteLine("");
            _output.WriteLine($"{"#",3} {"chart",-36} {"lvl",-4} {"borda",6} {"share",6} {"hold",5} {"best",5} {"played",6} {"med",-5} {"IQR",-11} {"you",-14} {"pool#"}");
            var ranked = borda.OrderByDescending(kv => kv.Value).ThenByDescending(kv => count[kv.Key]).ToArray();
            var place = 0;
            foreach (var (chartId, points) in ranked.Take(Top))
            {
                place++;
                var chart = charts[chartId];
                var played = peerScores.GetValueOrDefault(chartId, Array.Empty<int>());
                var median = played.Length == 0 ? "-" : Grade(Percentile(played, .5), mix);
                var iqr = played.Length == 0
                    ? "-"
                    : $"{Grade(Percentile(played, .25), mix)}–{Grade(Percentile(played, .75), mix)}";
                var you = myScores.TryGetValue(chartId, out var myScore)
                    ? $"{myScore} {Grade(myScore, mix)}" + (played.Length > 0 ? $" p{PercentileRank(played, myScore):F0}" : "")
                    : "unplayed";
                var inPool = myRank.TryGetValue(chartId, out var r) ? $"#{r}" : "";
                _output.WriteLine(
                    $"{place,3} {Trim(chart.Song.Name, 36),-36} {chart.Type.ToString()[0]}{(int)chart.Level,-3} {points,6} {100.0 * points / totalBorda,5:F1}% {count[chartId],5} {bestRank[chartId],5} {played.Length,6} {median,-5} {iqr,-11} {you,-14} {inPool}");
            }

            // How level-sorted is it? Borda mass by level, and the three orderings' top-50 overlap.
            _output.WriteLine("");
            _output.WriteLine("Borda mass by level: " + string.Join("  ", borda
                .GroupBy(kv => (int)charts[kv.Key].Level)
                .OrderBy(g => g.Key)
                .Select(g => $"L{g.Key}={100.0 * g.Sum(kv => kv.Value) / totalBorda:F0}%")));
            _output.WriteLine("Charts by level in the top 50 (Borda): " + string.Join("  ", ranked.Take(50)
                .GroupBy(kv => (int)charts[kv.Key].Level).OrderBy(g => g.Key).Select(g => $"L{g.Key}×{g.Count()}")));
            var topBorda = ranked.Take(50).Select(kv => kv.Key).ToHashSet();
            var topCount = count.OrderByDescending(kv => kv.Value).ThenByDescending(kv => borda[kv.Key]).Take(50)
                .Select(kv => kv.Key).ToHashSet();
            var topValue = value.OrderByDescending(kv => kv.Value).Take(50).Select(kv => kv.Key).ToHashSet();
            _output.WriteLine($"top-50 overlap: Borda∩Count {topBorda.Intersect(topCount).Count()}, Borda∩Value {topBorda.Intersect(topValue).Count()}, Count∩Value {topCount.Intersect(topValue).Count()}");

            // Variability: the IQR width (Q3 − Q1, points) among peers who played it, five or more
            // of them, σ-banded across the charts on the page — the tier-list technique applied to
            // spread rather than count. Raw and log, so the skew is visible before a cut is picked.
            var widths = peerScores
                .Where(kv => kv.Value.Length >= 5 && borda.ContainsKey(kv.Key))
                .Select(kv => (kv.Key, Width: Percentile(kv.Value, .75) - Percentile(kv.Value, .25)))
                .ToArray();
            if (widths.Length > 0)
            {
                _output.WriteLine("");
                _output.WriteLine($"Variability over {widths.Length} charts with ≥5 peer scores: IQR width min {widths.Min(w => w.Width):F0}, median {Percentile(widths.Select(w => (int)w.Width).OrderBy(x => x).ToArray(), .5):F0}, max {widths.Max(w => w.Width):F0}");
                foreach (var (label, transform) in new (string, Func<double, double>)[] { ("raw", w => w), ("log(1+w/1000)", w => Math.Log(1 + w / 1000.0)) })
                {
                    var values = widths.Select(w => transform(w.Width)).ToArray();
                    var mean = values.Average();
                    var sd = Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Length);
                    var skew = sd == 0 ? 0 : values.Sum(v => Math.Pow((v - mean) / sd, 3)) / values.Length;
                    string Band5(double v) => v < mean - 1.5 * sd ? "very consistent" : v < mean - .5 * sd ? "consistent" : v <= mean + .5 * sd ? "mixed" : v <= mean + 1.5 * sd ? "split" : "very split";
                    var bands = widths.GroupBy(w => Band5(transform(w.Width))).ToDictionary(g => g.Key, g => g.ToArray());
                    _output.WriteLine($"  {label,-14} skew {skew,5:F2}  →  " + string.Join("  ", new[] { "very consistent", "consistent", "mixed", "split", "very split" }
                        .Select(b => $"{b} {(bands.TryGetValue(b, out var arr) ? arr.Length : 0)}")));
                    foreach (var b in new[] { "very consistent", "consistent", "mixed", "split", "very split" })
                        if (bands.TryGetValue(b, out var arr))
                            _output.WriteLine($"      {b,-15} e.g. " + string.Join(", ", arr.OrderByDescending(w => borda[w.Key]).Take(4)
                                .Select(w => $"{Trim(charts[w.Key].Song.Name, 18)} {charts[w.Key].Type.ToString()[0]}{(int)charts[w.Key].Level} ({w.Width / 1000:F0}k)")));
                }
            }

            // What the Play page's target list would hold from the same evidence: the peers' median
            // priced as the projection prices it (ExpectedPlateForScore), against the type's own
            // bar — and how many of those targets are held in no peer's pool, i.e. would not appear
            // on a page built from pool holdings.
            var myRatingOf = byPlayer.GetValueOrDefault(userId, Array.Empty<UserPhoenixScore>())
                .ToDictionary(r => r.ChartId, r => scoring.GetScore(charts[r.ChartId], r.Score, r.Plate ?? PhoenixPlate.RoughGame, r.IsBroken));
            var typeBar = myPool != null ? myPool[^1].Rating : 0;
            var expectedValue = peerScores.Where(kv => kv.Value.Length >= 5)
                .ToDictionary(kv => kv.Key, kv =>
                {
                    var med = PhoenixScore.From((int)Math.Round(Percentile(kv.Value, .5)));
                    return scoring.GetScore(charts[kv.Key], med, ScoringConfiguration.ExpectedPlateForScore(med), false);
                });
            var targets = expectedValue
                .Select(kv => (kv.Key, Gain: kv.Value - Math.Max(myRatingOf.GetValueOrDefault(kv.Key), typeBar)))
                .Where(t => t.Gain > 0).OrderByDescending(t => t.Gain).ToArray();
            var top100 = targets.Take(100).ToArray();
            _output.WriteLine("");
            _output.WriteLine($"Play-page targets from this evidence (bar {typeBar:F2}): {targets.Length} charts pay; the page shows the top 100 → {top100.Length}; " +
                              $"of those {top100.Count(t => count.ContainsKey(t.Key))} are held by ≥1 peer, {top100.Count(t => !count.ContainsKey(t.Key))} by nobody; " +
                              $"held-by-nobody among ALL paying: {targets.Count(t => !count.ContainsKey(t.Key))}. " +
                              $"Your-Peers 'gains only' would show {borda.Keys.Count(id => expectedValue.TryGetValue(id, out var v) && v - Math.Max(myRatingOf.GetValueOrDefault(id), typeBar) > 0)} of {borda.Count} held charts; " +
                              $"held charts with no projection (<5 scored): {borda.Keys.Count(id => !expectedValue.ContainsKey(id))}.");
            _output.WriteLine("  top-100 targets held by nobody: " + string.Join(", ", top100.Where(t => !count.ContainsKey(t.Key)).Take(20)
                .Select(t => $"{Trim(charts[t.Key].Song.Name, 20)} {charts[t.Key].Type.ToString()[0]}{(int)charts[t.Key].Level} +{t.Gain:F1} ({peerScores[t.Key].Length} scored)")));
            _output.WriteLine("  top-10 targets: " + string.Join(", ", top100.Take(10)
                .Select(t => $"{Trim(charts[t.Key].Song.Name, 20)} {charts[t.Key].Type.ToString()[0]}{(int)charts[t.Key].Level} +{t.Gain:F1} hold {count.GetValueOrDefault(t.Key)}")));

            // The shipping page's own list for this pool, so the mock carries the REAL Play rows
            // (peer-projected AND carried Phoenix 1) and the probe's arithmetic can be checked
            // against them.
            var page = await services.GetRequiredService<IMediator>().Send(new GetPumbilityPageQuery(userId, mix, chartType), CancellationToken.None);
            var shipping = page.Targets;
            _output.WriteLine($"  shipping Play list ({chartType} pool): {shipping.Count} rows — {shipping.Count(t => t.Source == TargetSource.Peers)} peer-projected, {shipping.Count(t => t.Source == TargetSource.Phoenix1)} carried from Phoenix 1; " +
                              $"peer rows also in the probe's target set: {shipping.Count(t => t.Source == TargetSource.Peers && targets.Any(x => x.Key == t.ChartId))}; carried rows held by ≥1 peer: {shipping.Count(t => t.Source == TargetSource.Phoenix1 && count.ContainsKey(t.ChartId))}");

            // Mock export: everything a page mock renders from, when SCORETRACKER_PROBE_OUT names a
            // directory. Tiers come from the real processor (log-scaled, the PUMBILITY lens's rule);
            // variability from the five log bands above; the roster carries only what the page would.
            if (Environment.GetEnvironmentVariable("SCORETRACKER_PROBE_OUT") is { Length: > 0 } outDir)
            {
                var tiers = TierListProcessor.ProcessIntoLogScaledTierList("Peers", borda)
                    .ToDictionary(e => e.ChartId, e => e.Category.ToString());
                var logWidths = widths.ToDictionary(w => w.Key, w => Math.Log(1 + w.Width / 1000.0));
                var lm = logWidths.Count == 0 ? 0 : logWidths.Values.Average();
                var lsd = logWidths.Count == 0 ? 0 : Math.Sqrt(logWidths.Values.Sum(v => (v - lm) * (v - lm)) / logWidths.Count);
                string? Variability(Guid id) => !logWidths.TryGetValue(id, out var v) ? null
                    : v < lm - 1.5 * lsd ? "Very consistent" : v < lm - .5 * lsd ? "Consistent"
                    : v <= lm + .5 * lsd ? "Mixed" : v <= lm + 1.5 * lsd ? "Split" : "Very split";
                var peerStats = (await stats.GetStats(mix, peers, CancellationToken.None)).ToDictionary(s => s.UserId);
                var nameOf = records.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => (Name: g.First().UserName.ToString(), g.First().IsPublic));
                var myPoolSet = myPool?.Select(c => c.ChartId).ToHashSet() ?? new HashSet<Guid>();
                var export = new
                {
                    viewer = new { userId, total = mine.SkillRating, rung = rung.Index, gem = rung.Gem?.ToString(), level = rung.Level,
                        singlesLevel = mine.SinglesCompetitiveLevel, doublesLevel = mine.DoublesCompetitiveLevel,
                        window = new { pool = myPoolTotal, lowest = myPoolTotal - PeerGroup.PumbilityWindowBelow, highest = myPoolTotal + PeerGroup.PumbilityWindowAbove } },
                    chartType = chartType.ToString(),
                    viewerLit,
                    viewerRecords = byPlayer.GetValueOrDefault(userId)?.Length ?? 0,
                    peerCount = peers.Count,
                    privatePeers = peers.Count(p => nameOf.TryGetValue(p, out var n) && !n.IsPublic),
                    charts = ranked.Select((kv, i) =>
                    {
                        var c = charts[kv.Key];
                        var played = peerScores.GetValueOrDefault(kv.Key, Array.Empty<int>());
                        return new
                        {
                            id = kv.Key, name = c.Song.Name.ToString(), type = c.Type.ToString(), level = (int)c.Level,
                            songType = c.Song.Type.ToString(), image = c.Song.ImagePath.ToString(),
                            rank = i + 1, borda = kv.Value, hold = count[kv.Key], bestRank = bestRank[kv.Key], played = played.Length,
                            median = played.Length >= 5 ? (int?)Math.Round(Percentile(played, .5)) : null,
                            q1 = played.Length >= 5 ? (int?)Math.Round(Percentile(played, .25)) : null,
                            q3 = played.Length >= 5 ? (int?)Math.Round(Percentile(played, .75)) : null,
                            variability = Variability(kv.Key),
                            tier = tiers[kv.Key],
                            myScore = myScores.TryGetValue(kv.Key, out var s) ? (int?)s : null,
                            myPercentile = myScores.TryGetValue(kv.Key, out var s2) && played.Length > 0 ? (double?)PercentileRank(played, s2) : null,
                            myPoolRank = myRank.TryGetValue(kv.Key, out var r) ? (int?)r : null,
                            myRating = myRatingOf.TryGetValue(kv.Key, out var mr) ? (double?)mr : null,
                            expectedValue = expectedValue.TryGetValue(kv.Key, out var ev) ? (double?)ev : null
                        };
                    }).ToArray(),
                    // Targets the Play page would list that no peer HOLDS (they scored it, it just
                    // never made a pool) — the rows a holdings-based page would lose.
                    unheldTargets = top100.Where(t => !count.ContainsKey(t.Key)).Select(t => new
                    {
                        id = t.Key, name = charts[t.Key].Song.Name.ToString(), type = charts[t.Key].Type.ToString(), level = (int)charts[t.Key].Level,
                        gain = t.Gain, played = peerScores[t.Key].Length, median = (int)Math.Round(Percentile(peerScores[t.Key], .5)),
                        expectedValue = expectedValue[t.Key], myScore = myScores.TryGetValue(t.Key, out var us) ? (int?)us : null,
                        variability = Variability(t.Key)
                    }).ToArray(),
                    typeBar,
                    // The viewer's To-Do list, so the mock can wear the tier list's dashed-blue ring.
                    toDo = (await services.GetRequiredService<IChartListRepository>().GetSavedChartsByUser(userId, CancellationToken.None))
                        .Where(sc => sc.ListType == ChartListType.ToDo).Select(sc => sc.ChartId).Distinct().ToArray(),
                    playList = shipping.Select(t => new
                    {
                        id = t.ChartId, name = charts[t.ChartId].Song.Name.ToString(), type = charts[t.ChartId].Type.ToString(), level = (int)charts[t.ChartId].Level,
                        gain = t.Gain, projected = (int)t.Projected, source = t.Source.ToString(), current = t.Current == null ? (int?)null : (int)t.Current.Value,
                        held = count.GetValueOrDefault(t.ChartId),
                        myRating = myRatingOf.TryGetValue(t.ChartId, out var pr) ? (double?)pr : null,
                        played = peerScores.TryGetValue(t.ChartId, out var pp) ? pp.Length : 0,
                        median = peerScores.TryGetValue(t.ChartId, out var pm) && pm.Length >= 5 ? (int?)Math.Round(Percentile(pm, .5)) : null,
                        variability = Variability(t.ChartId)
                    }).ToArray(),
                    yoursAlone = (myPool ?? Array.Empty<(Guid, double)>()).Where(c => !count.ContainsKey(c.ChartId)).Select(c => new
                    {
                        id = c.ChartId, name = charts[c.ChartId].Song.Name.ToString(), type = charts[c.ChartId].Type.ToString(),
                        level = (int)charts[c.ChartId].Level, image = charts[c.ChartId].Song.ImagePath.ToString(),
                        myScore = myScores[c.ChartId], myPoolRank = myRank[c.ChartId], rating = c.Rating
                    }).ToArray(),
                    // The viewer's priced records of the type in rank order (a short pool included), so
                    // the frame's totals and bar can be rebuilt for All / Singles / Doubles.
                    myPool = byPlayer.GetValueOrDefault(userId, Array.Empty<UserPhoenixScore>())
                        .Select(r => (r.ChartId, Rating: scoring.GetScore(charts[r.ChartId], r.Score, r.Plate ?? PhoenixPlate.RoughGame, r.IsBroken), Score: (int)r.Score, r.Plate))
                        .Where(r => r.Rating > 0).OrderByDescending(r => r.Rating).Take(PeerGroup.PumbilityPoolSize)
                        .Select(r => new { id = r.ChartId, name = charts[r.ChartId].Song.Name.ToString(), type = charts[r.ChartId].Type.ToString(), level = (int)charts[r.ChartId].Level, rating = r.Rating, score = r.Score, plate = r.Plate?.ToString() })
                        .ToArray(),
                    myPoolOverlapWithTop50 = myPool?.Count(c => topBorda.Contains(c.ChartId)),
                    myPoolHeldByAtMostOne = myPool?.Count(c => count.GetValueOrDefault(c.ChartId) <= 1),
                    myPoolLevels = myPool?.GroupBy(c => (int)charts[c.ChartId].Level).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                    bandMassByLevel = borda.GroupBy(kv => (int)charts[kv.Key].Level).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => Math.Round(100.0 * g.Sum(kv => kv.Value) / totalBorda, 1)),
                    roster = peers.Select(p => new
                    {
                        userId = p, name = nameOf[p].Name, isPublic = nameOf[p].IsPublic,
                        total = peerStats.TryGetValue(p, out var ps) ? ps.SkillRating : 0,
                        rung = peerStats.TryGetValue(p, out var ps2) ? Phoenix2PumbilityLevel.From(ps2.SkillRating).Index : 0,
                        gem = peerStats.TryGetValue(p, out var ps3) ? Phoenix2PumbilityLevel.From(ps3.SkillRating).Gem?.ToString() : null,
                        gemLevel = peerStats.TryGetValue(p, out var ps4) ? Phoenix2PumbilityLevel.From(ps4.SkillRating).Level : null,
                        singlesLevel = peerStats.TryGetValue(p, out var ps5) ? ps5.SinglesCompetitiveLevel : 0,
                        doublesLevel = peerStats.TryGetValue(p, out var ps6) ? ps6.DoublesCompetitiveLevel : 0,
                        overlap = pools[p].Count(c => myPoolSet.Contains(c.ChartId))
                    }).OrderByDescending(r => r.total).ToArray(),
                    boards = ranked.Take(3).Select(kv => kv.Key)
                        .Concat(ranked.Take(40).Select(kv => kv.Key).Where(myScores.ContainsKey).Take(3))
                        .Distinct()
                        .Select(id => new
                        {
                            chartId = id, name = charts[id].Song.Name.ToString(), type = charts[id].Type.ToString(), level = (int)charts[id].Level,
                            rows = records.Where(r => r.ChartId == id && (peers.Contains(r.UserId) || r.UserId == userId))
                                .OrderByDescending(r => (int)r.Score)
                                .Select(r => new { r.UserId, name = r.UserName.ToString(), r.IsPublic, score = (int)r.Score, plate = r.Plate?.ToString(), isMe = r.UserId == userId })
                                .ToArray()
                        }).ToArray()
                };
                Directory.CreateDirectory(outDir);
                var path = Path.Combine(outDir, $"peers-{chartType}.json");
                await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(export,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = false }));
                _output.WriteLine($"exported {path}");
            }

            // Where the viewer's own pool sits against the band's consensus.
            if (myPool != null)
            {
                var consensusRankOf = ranked.Select((kv, i) => (kv.Key, Rank: i + 1)).ToDictionary(x => x.Key, x => x.Rank);
                var mineInTop50 = myPool.Count(c => topBorda.Contains(c.ChartId));
                var mineHeldByNobody = myPool.Count(c => !count.ContainsKey(c.ChartId));
                _output.WriteLine($"your pool vs the band: {mineInTop50}/50 of your charts are in the band's Borda top 50; {mineHeldByNobody} of yours are in no peer's pool");
                _output.WriteLine("band top-50 charts you do NOT hold in your pool: " + string.Join(", ", ranked.Take(50)
                    .Where(kv => !myRank.ContainsKey(kv.Key))
                    .Select(kv => $"{Trim(charts[kv.Key].Song.Name, 22)} {charts[kv.Key].Type.ToString()[0]}{(int)charts[kv.Key].Level}" +
                                  (myScores.TryGetValue(kv.Key, out var s) ? $" (you {Grade(s, mix)})" : " (unplayed)"))));
                _output.WriteLine("your pool charts held by ≤1 peer: " + string.Join(", ", myPool
                    .Where(c => count.GetValueOrDefault(c.ChartId) <= 1)
                    .Select(c => $"{Trim(charts[c.ChartId].Song.Name, 22)} {charts[c.ChartId].Type.ToString()[0]}{(int)charts[c.ChartId].Level} (#{myRank[c.ChartId]}, band #{consensusRankOf.GetValueOrDefault(c.ChartId, 0)})")));
            }
        }

        // The peers' average singles/doubles split of their MERGED fifty — what the Breakdown
        // page's second composition bar draws (PUMBILITY doc, round nine part two). Each peer's
        // records of both types, priced, merged, the top fifty taken and split by type; then the
        // average over the peers who hold a full fifty. Mock export only, read-only like the rest.
        if (unionPeers.Count > 0 && Environment.GetEnvironmentVariable("SCORETRACKER_PROBE_OUT") is { Length: > 0 } splitDir)
        {
            var byPeer = new Dictionary<Guid, List<(ChartType Type, double Rating)>>();
            foreach (var type in new[] { ChartType.Single, ChartType.Double })
            foreach (var r in await scores.GetPlayerScoresInLevelRange(mix, unionPeers, type,
                         PeerGroup.PumbilityPoolFloor, DifficultyLevel.Max, CancellationToken.None))
            {
                if (!charts.TryGetValue(r.ChartId, out var chart)) continue;
                var rating = scoring.GetScore(chart, r.Score, r.Plate ?? PhoenixPlate.RoughGame, r.IsBroken);
                if (rating <= 0) continue;
                if (!byPeer.TryGetValue(r.UserId, out var list)) byPeer[r.UserId] = list = new List<(ChartType, double)>();
                list.Add((type, rating));
            }

            var splits = byPeer.Values
                .Select(list => list.OrderByDescending(x => x.Rating).Take(PeerGroup.PumbilityPoolSize).ToArray())
                .Where(top => top.Length == PeerGroup.PumbilityPoolSize)
                .Select(top => new
                {
                    singlesCount = top.Count(x => x.Type == ChartType.Single),
                    doublesCount = top.Count(x => x.Type == ChartType.Double),
                    singlesValue = top.Where(x => x.Type == ChartType.Single).Sum(x => x.Rating),
                    doublesValue = top.Where(x => x.Type == ChartType.Double).Sum(x => x.Rating)
                })
                .ToArray();
            var split = new
            {
                peers = splits.Length,
                singlesCount = splits.Length == 0 ? 0 : splits.Average(x => x.singlesCount),
                doublesCount = splits.Length == 0 ? 0 : splits.Average(x => x.doublesCount),
                singlesValue = splits.Length == 0 ? 0 : splits.Average(x => x.singlesValue),
                doublesValue = splits.Length == 0 ? 0 : splits.Average(x => x.doublesValue)
            };
            var splitPath = Path.Combine(splitDir, "peers-split.json");
            await File.WriteAllTextAsync(splitPath, System.Text.Json.JsonSerializer.Serialize(split));
            _output.WriteLine($"peers' merged-fifty split over {split.peers} peers: singles {split.singlesValue:F2} ({split.singlesCount:F1}) · doubles {split.doublesValue:F2} ({split.doublesCount:F1}) → {splitPath}");
        }

        Assert.True(true, "a measurement, not a guarantee — read the output");
    }

    private static string Trim(string s, int width) => s.Length <= width ? s : s[..(width - 1)] + "…";

    private static string Grade(double score, MixEnum mix) =>
        PhoenixScore.From((int)Math.Round(score)).LetterGradeFor(mix).ToString();

    private static double Percentile(int[] sorted, double q)
    {
        if (sorted.Length == 1) return sorted[0];
        var pos = q * (sorted.Length - 1);
        var lo = (int)Math.Floor(pos);
        var hi = Math.Min(lo + 1, sorted.Length - 1);
        return sorted[lo] + (pos - lo) * (sorted[hi] - sorted[lo]);
    }

    private static double PercentileRank(int[] sorted, int score) =>
        100.0 * sorted.Count(s => s < score) / sorted.Length;

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
