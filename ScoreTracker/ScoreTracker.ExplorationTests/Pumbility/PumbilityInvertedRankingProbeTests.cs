using MassTransit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Wiring;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Wiring;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.Records;
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
///     Workshop probe for an inverted PUMBILITY list: which folders a player's PUMBILITY peers
///     actually build their pools from, and — inside those folders — every chart, ranked by how
///     FEW of the peers keep it in their top 50. The peers are the Play page's own (the
///     projector's call as the projection saga makes it, board peers included), so the counts
///     here are the counts the page would print.
///     <para>
///         Configure <c>CatalogProbe:ConnectionString</c> or SCORETRACKER_CATALOG_CONNECTION;
///         SCORETRACKER_PUMBILITY_PROBE_USER picks the player; SCORETRACKER_PROBE_OUT names a
///         directory for the mock's JSON. Read-only.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class PumbilityInvertedRankingProbeTests
{
    private readonly ITestOutputHelper _output;

    public PumbilityInvertedRankingProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [CatalogProbeFact]
    public async Task One_players_folders_and_least_kept_charts()
    {
        await using var services = BuildServices();
        var mediator = services.GetRequiredService<IMediator>();
        var projector = services.GetRequiredService<IScoreProjector>();
        var scores = services.GetRequiredService<IScoreReader>();
        var chartRepository = services.GetRequiredService<IChartRepository>();
        var statsRepository = services.GetRequiredService<IPlayerStatsRepository>();
        const MixEnum mix = MixEnum.Phoenix2;

        var userId = ProbeUserId ??
                     (await statsRepository.GetUserIdsWithStats(mix, CancellationToken.None)).First();
        var charts = (await chartRepository.GetCharts(mix, cancellationToken: CancellationToken.None))
            .ToDictionary(c => c.Id);
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);

        // The frame's three reads at Great: totals, bars, the pools and the gains per pool.
        var pages = new Dictionary<string, PumbilityPageRecord>();
        foreach (var (key, pool) in new (string, ChartType?)[]
                     { ("All", null), ("Single", ChartType.Single), ("Double", ChartType.Double) })
            pages[key] = await mediator.Send(new GetPumbilityPageQuery(userId, mix, pool, Energy.Great),
                CancellationToken.None);

        var mine = (await scores.GetBestScores(mix, userId, CancellationToken.None))
            .Where(r => r.Score != null && !r.IsBroken && charts.ContainsKey(r.ChartId))
            .ToDictionary(r => r.ChartId);
        var toDo = (await services.GetRequiredService<IChartListRepository>()
                .GetSavedChartsByUser(userId, CancellationToken.None))
            .Where(sc => sc.ListType == ChartListType.ToDo).Select(sc => sc.ChartId).ToHashSet();
        var floor = Math.Min(pages["Single"].Bar ?? 0, pages["Double"].Bar ?? 0);
        var allPoolRank = pages["All"].Pool.ToDictionary(p => p.ChartId, p => p.Place);
        var gainsAll = pages["All"].Targets.GroupBy(t => t.ChartId).ToDictionary(g => g.Key, g => g.First());

        var export = new Dictionary<string, object?>
        {
            ["viewer"] = new
            {
                userId,
                totals = pages["All"].Totals,
                bars = pages.ToDictionary(kv => kv.Key, kv => new
                {
                    bar = kv.Value.Bar,
                    chart = kv.Value.BarChartId is { } id && charts.TryGetValue(id, out var c)
                        ? new { name = c.Song.Name.ToString(), type = c.Type.ToString(), level = (int)c.Level }
                        : null,
                    count = kv.Value.Pool.Count,
                    total = kv.Value.Total
                }),
                allPool = pages["All"].Pool.Select(p => new
                {
                    p.Place, id = p.ChartId, name = charts[p.ChartId].Song.Name.ToString(),
                    type = charts[p.ChartId].Type.ToString(), level = (int)charts[p.ChartId].Level,
                    score = (int)p.Score, plate = p.Plate?.ToString(), p.Value
                }).ToArray()
            }
        };

        foreach (var type in new[] { ChartType.Single, ChartType.Double })
        {
            var key = type.ToString();
            var page = pages[key];
            _output.WriteLine("");
            _output.WriteLine($"===== {type} · pool {page.Total:F2} · {page.Pool.Count} charts · bar {page.Bar:F2} =====");

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
                _output.WriteLine("dark, or no pools came back");
                continue;
            }

            var peerCount = summary.Peers.Count;
            _output.WriteLine($"peers {peerCount} (board {group.BoardSize}) · window {group.Lowest:F0}–{group.Highest:F0} · below floor {group.AnsweredBelowFloor}");

            // Reach: how many peers hold at least one chart of the level in their fifty.
            var reach = new Dictionary<int, int>();
            foreach (var held in summary.Pools.Values)
            foreach (var level in held.Where(charts.ContainsKey).Select(id => (int)charts[id].Level).Distinct())
                reach[level] = reach.GetValueOrDefault(level) + 1;

            // Played, exactly, over the account peers — the summary keeps a chart nobody holds only
            // once five have scored it, and the inverted list is exactly the charts under that.
            var accountPeers = summary.Peers.Where(p => p.UserId != null).Select(p => p.UserId!.Value).ToHashSet();
            var playedByAccounts = (await scores.GetPlayerScoresInLevelRange(mix, accountPeers, type,
                    PeerGroup.PumbilityPoolFloor, DifficultyLevel.Max, CancellationToken.None))
                .Where(r => !r.IsBroken && charts.ContainsKey(r.ChartId))
                .GroupBy(r => r.ChartId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.UserId).Distinct().Count());

            var typePoolRank = page.Pool.ToDictionary(p => p.ChartId, p => p.Place);
            var gainsType = page.Targets.GroupBy(t => t.ChartId).ToDictionary(g => g.Key, g => g.First());
            var heldPoints = summary.Charts.Where(kv => kv.Value.Holders > 0).ToDictionary(kv => kv.Key, kv => kv.Value.Points);
            var tiers = TierListProcessor.ProcessIntoLogScaledTierList("Prevalence", heldPoints)
                .ToDictionary(e => e.ChartId, e => e.Category.ToString());
            var totalSlots = summary.Charts.Values.Sum(e => e.Holders);

            var levels = charts.Values.Where(c => c.Type == type && (int)c.Level >= 10)
                .Select(c => (int)c.Level).Distinct().OrderBy(l => l).ToArray();
            var folders = levels.Select(level =>
            {
                var inFolder = charts.Values.Where(c => c.Type == type && (int)c.Level == level).ToArray();
                var entries = inFolder.Select(c => summary.Charts.GetValueOrDefault(c.Id)).ToArray();
                return new
                {
                    level,
                    catalog = inFolder.Length,
                    held = entries.Count(e => e is { Holders: > 0 }),
                    slots = entries.Sum(e => e?.Holders ?? 0),
                    points = entries.Sum(e => e?.Points ?? 0),
                    reach = reach.GetValueOrDefault(level),
                    played = inFolder.Count(c => playedByAccounts.ContainsKey(c.Id)),
                    minePool = inFolder.Count(c => typePoolRank.ContainsKey(c.Id)),
                    minePassed = inFolder.Count(c => mine.ContainsKey(c.Id))
                };
            }).ToArray();

            _output.WriteLine($"{"lvl",4} {"charts",6} {"held",5} {"slots%",7} {"reach%",7} {"played",6} {"yours",6}");
            foreach (var f in folders.Where(f => f.slots > 0 || f.minePool > 0))
                _output.WriteLine($"{f.level,4} {f.catalog,6} {f.held,5} {100.0 * f.slots / Math.Max(1, totalSlots),6:F1}% {100.0 * f.reach / Math.Max(1, peerCount),6:F0}% {f.played,6} {f.minePool,6}");

            var rows = charts.Values.Where(c => c.Type == type && (int)c.Level >= 10).Select(c =>
            {
                var entry = summary.Charts.GetValueOrDefault(c.Id);
                var record = mine.GetValueOrDefault(c.Id);
                var myScore = record?.Score;
                var target = gainsType.GetValueOrDefault(c.Id);
                var targetAll = gainsAll.GetValueOrDefault(c.Id);
                int? At(Energy energy) => entry?.ProjectedAt(energy.Quantile()) is { } projected ? (int)projected : null;
                return new
                {
                    id = c.Id,
                    name = c.Song.Name.ToString(),
                    level = (int)c.Level,
                    songType = c.Song.Type.ToString(),
                    holders = entry?.Holders ?? 0,
                    points = entry?.Points ?? 0,
                    scored = entry?.Scored,
                    played = playedByAccounts.GetValueOrDefault(c.Id),
                    tier = tiers.GetValueOrDefault(c.Id),
                    good = At(Energy.Good),
                    great = At(Energy.Great),
                    top = At(Energy.TopOfMyGame),
                    myScore = myScore is { } ms ? (int?)(int)ms : null,
                    myPlate = record?.Plate?.ToString(),
                    myPct = myScore is { } pctScore && entry != null ? entry.PercentileOf((int)pctScore) : null,
                    myValue = myScore is { } valueScore
                        ? (double?)scoring.GetScore(c, valueScore, record!.Plate ?? PhoenixPlate.RoughGame, false)
                        : null,
                    rankType = typePoolRank.TryGetValue(c.Id, out var rankOfType) ? (int?)rankOfType : null,
                    rankAll = allPoolRank.TryGetValue(c.Id, out var rankOfAll) ? (int?)rankOfAll : null,
                    gainType = target?.Gain,
                    carriedType = target?.Source == TargetSource.Phoenix1,
                    projectedType = target == null ? null : (int?)(int)target.Projected,
                    gainAll = targetAll?.Gain,
                    carriedAll = targetAll?.Source == TargetSource.Phoenix1,
                    toDo = toDo.Contains(c.Id)
                };
            }).ToArray();

            // A first look at the inverse, inside the folders at least half the peers reach.
            var inReach = folders.Where(f => f.reach * 2 >= peerCount).Select(f => f.level).ToHashSet();
            var inverted = rows.Where(r => inReach.Contains(r.level))
                .OrderBy(r => r.holders).ThenBy(r => r.points).ThenBy(r => r.played).ThenByDescending(r => r.level)
                .ToArray();
            _output.WriteLine("");
            _output.WriteLine($"folders reached by half the peers: {string.Join(", ", inReach.OrderBy(l => l))} → {inverted.Length} charts, {inverted.Count(r => r.holders == 0)} kept by nobody, {inverted.Count(r => r.holders == 0 && r.played == 0)} played by no account peer");
            foreach (var r in inverted.Take(30))
                _output.WriteLine($"  {type.ToString()[0]}{r.level} {Trim(r.name, 34),-34} kept {r.holders,2} · played {r.played,2} · you {(r.myScore?.ToString() ?? "-"),7}{(r.gainType is { } g ? $" · +{g:F1}" : "")}");

            export[key] = new
            {
                group = new
                {
                    size = group.Size, boardSize = group.BoardSize, boardAsOf = group.BoardAsOf,
                    center = group.Center, lowest = group.Lowest, highest = group.Highest,
                    belowFloor = group.AnsweredBelowFloor, estimate = group.PlacedByEstimate
                },
                peerCount,
                accountPeers = accountPeers.Count,
                totalSlots,
                folders,
                charts = rows
            };
        }

        if (Environment.GetEnvironmentVariable("SCORETRACKER_PROBE_OUT") is { Length: > 0 } outDir)
        {
            Directory.CreateDirectory(outDir);
            var path = Path.Combine(outDir, "inverted.json");
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(export));
            _output.WriteLine($"exported {path}");
        }

        Assert.True(true, "a measurement, not a guarantee — read the output");
    }

    /// <summary>
    ///     The same folder question on Phoenix 1, whose peers are the competitive band rather than a
    ///     window of full pools: a band holds players with a handful of charts, so the reach is
    ///     read two ways — against every peer, and against the peers who hold anything at all.
    /// </summary>
    [CatalogProbeFact]
    public async Task Phoenix1_folders_in_reach()
    {
        await using var services = BuildServices();
        var mediator = services.GetRequiredService<IMediator>();
        var projector = services.GetRequiredService<IScoreProjector>();
        var chartRepository = services.GetRequiredService<IChartRepository>();
        var statsRepository = services.GetRequiredService<IPlayerStatsRepository>();
        const MixEnum mix = MixEnum.Phoenix;

        var userId = ProbeUserId ??
                     (await statsRepository.GetUserIdsWithStats(mix, CancellationToken.None)).First();
        var charts = (await chartRepository.GetCharts(mix, cancellationToken: CancellationToken.None))
            .ToDictionary(c => c.Id);
        var scoringLevels = await mediator.Send(new GetChartScoringLevelsQuery(mix), CancellationToken.None);
        var export = new Dictionary<string, object?>();

        foreach (var type in new[] { ChartType.Single, ChartType.Double })
        {
            var myLevel = await projector.CompetitiveLevel(mix, type, userId, CancellationToken.None);
            _output.WriteLine("");
            _output.WriteLine($"===== Phoenix 1 {type} · competitive level {myLevel:F2} =====");
            if (myLevel <= 1)
            {
                _output.WriteLine("no competitive level, no band");
                continue;
            }

            // The projection saga's Phoenix 1 candidates: scoring level within two of the player's.
            var scoped = charts.Values.Where(c => c.Type == type)
                .Where(c => Math.Abs((scoringLevels.TryGetValue(c.Id, out var scoring) ? scoring : (int)c.Level) - myLevel) <= 2.0)
                .Select(c => new ProjectionTarget(c.Id, (int)c.Level))
                .ToArray();
            var projection = await projector.Project(new ScoreProjectionRequest(mix, type, userId, scoped,
                PeerEstimator.CompetitiveWindow, charts, RelaxFloorWhenEmpty: true,
                Quantiles: EnergyRungs.All), CancellationToken.None);
            if (projection.PeerPools is not { } summary)
            {
                _output.WriteLine("no pools came back");
                continue;
            }

            var peerCount = summary.Peers.Count;
            var sizes = summary.Pools.Values.Select(p => p.Count).OrderBy(n => n).ToArray();
            var holding = sizes.Count(n => n > 0);
            int SizeAt(double q) => sizes.Length == 0 ? 0 : sizes[(int)(q * (sizes.Length - 1))];
            _output.WriteLine($"band {peerCount} · holding anything {holding} · pool sizes p10 {SizeAt(.1)} p25 {SizeAt(.25)} p50 {SizeAt(.5)} p90 {SizeAt(.9)} · full {sizes.Count(n => n >= 50)} · under ten {sizes.Count(n => n < 10)}");

            var reach = new Dictionary<int, int>();
            foreach (var held in summary.Pools.Values)
            foreach (var level in held.Where(charts.ContainsKey).Select(id => (int)charts[id].Level).Distinct())
                reach[level] = reach.GetValueOrDefault(level) + 1;
            var totalSlots = summary.Charts.Values.Sum(e => e.Holders);

            _output.WriteLine($"{"lvl",4} {"charts",6} {"held",5} {"slots%",7} {"reach/all",9} {"reach/holding",13}");
            foreach (var level in reach.Keys.OrderBy(l => l))
            {
                var inFolder = charts.Values.Where(c => c.Type == type && (int)c.Level == level).ToArray();
                var slots = inFolder.Sum(c => summary.Charts.GetValueOrDefault(c.Id)?.Holders ?? 0);
                _output.WriteLine($"{level,4} {inFolder.Length,6} {inFolder.Count(c => summary.Charts.GetValueOrDefault(c.Id) is { Holders: > 0 }),5} {100.0 * slots / Math.Max(1, totalSlots),6:F1}% {100.0 * reach[level] / Math.Max(1, peerCount),8:F0}% {100.0 * reach[level] / Math.Max(1, holding),12:F0}%");
            }

            IReadOnlyList<int> LevelsAgainst(int denominator) => denominator == 0
                ? Array.Empty<int>()
                : reach.Where(kv => kv.Value * 2 >= denominator).Select(kv => kv.Key).OrderBy(l => l).ToArray();
            var againstAll = LevelsAgainst(peerCount);
            var againstHolding = LevelsAgainst(holding);
            var inReach = againstHolding.ToHashSet();
            var inRange = charts.Values.Where(c => c.Type == type && inReach.Contains((int)c.Level)).ToArray();
            var unheld = inRange.Count(c => summary.Charts.GetValueOrDefault(c.Id) is not { Holders: > 0 });
            _output.WriteLine($"levels against every peer: {string.Join(", ", againstAll)} · against peers holding anything: {string.Join(", ", againstHolding)} → {inRange.Length} charts, {unheld} kept by nobody");

            export[type.ToString()] = new
            {
                competitiveLevel = myLevel,
                peerCount,
                holding,
                poolSizes = new { p10 = SizeAt(.1), p25 = SizeAt(.25), p50 = SizeAt(.5), p90 = SizeAt(.9), full = sizes.Count(n => n >= 50), underTen = sizes.Count(n => n < 10) },
                reach,
                againstAll,
                againstHolding,
                inRange = inRange.Length,
                unheld
            };
        }

        if (Environment.GetEnvironmentVariable("SCORETRACKER_PROBE_OUT") is { Length: > 0 } outDir)
        {
            Directory.CreateDirectory(outDir);
            var path = Path.Combine(outDir, "inverted-p1.json");
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(export));
            _output.WriteLine($"exported {path}");
        }

        Assert.True(true, "a measurement, not a guarantee — read the output");
    }

    private static string Trim(string s, int width)
    {
        return s.Length <= width ? s : s[..(width - 1)] + "…";
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
