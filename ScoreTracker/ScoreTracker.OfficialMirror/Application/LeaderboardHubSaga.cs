using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     The hub's read side. Everything resolves against the latest sealed snapshot;
///     per-player aggregates (board counts, #1s, computed ratings, archetypes) compute once
///     per snapshot from one placement pull and cache until the next seal — snapshots are
///     immutable, so a snapshot-keyed cache can never stale.
/// </summary>
internal sealed class LeaderboardHubSaga :
    IRequestHandler<GetWeeklyHighlightsQuery, WeeklyHighlightsRecord?>,
    IRequestHandler<GetOfficialRankingsQuery, OfficialRankingsRecord>,
    IRequestHandler<GetOfficialPlayerProfileQuery, OfficialPlayerProfileRecord?>,
    IRequestHandler<GetOfficialPlayerNamesQuery, IReadOnlyList<string>>,
    IRequestHandler<GetOfficialPopularityQuery, IReadOnlyList<OfficialPopularityRecord>>,
    IRequestHandler<GetImportRunsQuery, IReadOnlyList<ImportRunRecord>>,
    IRequestHandler<GetWhatItTakesQuery, WhatItTakesRecord>
{
    private const string PumbilityAll = "PUMBILITY";
    private const string PumbilitySingles = "PUMBILITY Singles";
    private const string PumbilityDoubles = "PUMBILITY Doubles";

    private readonly IOfficialSnapshotRepository _snapshots;
    private readonly IOfficialRecordRepository _records;
    private readonly IScoreReader _scores;
    private readonly IChartRepository _charts;
    private readonly IMemoryCache _cache;

    public LeaderboardHubSaga(IOfficialSnapshotRepository snapshots, IOfficialRecordRepository records,
        IScoreReader scores, IChartRepository charts, IMemoryCache cache)
    {
        _snapshots = snapshots;
        _records = records;
        _scores = scores;
        _charts = charts;
        _cache = cache;
    }

    public async Task<WeeklyHighlightsRecord?> Handle(GetWeeklyHighlightsQuery request,
        CancellationToken cancellationToken)
    {
        var latest = await _snapshots.GetLatestSealed(request.Mix, cancellationToken);
        if (latest?.CompletedAt == null) return null;

        var previous = await _snapshots.GetSealedBefore(request.Mix, latest.Id, cancellationToken);
        var highlights = await _records.GetHighlights(latest.Id, cancellationToken);
        var players = (await _snapshots.GetPlayersByIds(
                highlights.SelectMany(h => new[] { h.PlayerId, h.DethronedPlayerId ?? h.PlayerId })
                    .Distinct().ToArray(), cancellationToken))
            .ToDictionary(p => p.Id);

        OfficialPlayerRecord? Resolve(int? playerId)
        {
            return playerId != null && players.TryGetValue(playerId.Value, out var player)
                ? ToRecord(player)
                : null;
        }

        var movers = highlights.Where(h => h.Kind == HighlightKinds.PumbilityMover)
            .OrderBy(h => h.SortOrder)
            .Select(h => new OfficialMoverRecord(Resolve(h.PlayerId)!, (int)h.PrevValue!, (int)h.NewValue!,
                h.Score ?? 0))
            .ToArray();
        var climbed = highlights.Where(h => h.Kind == HighlightKinds.BoardsClimbed)
            .OrderBy(h => h.SortOrder)
            .Select(h => new OfficialBoardsClimbedRecord(Resolve(h.PlayerId)!, (int)h.NewValue!,
                (int)h.PrevValue!))
            .ToArray();
        var firsts = highlights
            .Where(h => h.Kind is HighlightKinds.FolderGradeFirst or HighlightKinds.ChartGradeFirst)
            .OrderByDescending(h => h.Kind == HighlightKinds.FolderGradeFirst).ThenBy(h => h.SortOrder)
            .Select(h => new OfficialGradeFirstRecord(Resolve(h.PlayerId)!, h.ChartId, h.ChartType, h.Level,
                h.GradeBand!, (int)h.Score!, h.Kind == HighlightKinds.FolderGradeFirst))
            .ToArray();
        var numberOnes = highlights.Where(h => h.Kind == HighlightKinds.NewNumberOne)
            .OrderBy(h => h.SortOrder)
            .Where(h => h.ChartId != null)
            .Select(h => new OfficialNewNumberOneRecord(Resolve(h.PlayerId)!, h.ChartId!.Value, (int)h.Score!,
                Resolve(h.DethronedPlayerId)))
            .ToArray();

        return new WeeklyHighlightsRecord(latest.CompletedAt.Value, previous?.CompletedAt, movers, climbed,
            firsts, numberOnes);
    }

    public async Task<OfficialRankingsRecord> Handle(GetOfficialRankingsQuery request,
        CancellationToken cancellationToken)
    {
        var latest = await _snapshots.GetLatestSealed(request.Mix, cancellationToken);
        if (latest?.CompletedAt == null)
            return new OfficialRankingsRecord(null, false, Array.Empty<OfficialRankingRecord>());

        var stats = await GetSnapshotStats(request.Mix, latest.Id, cancellationToken);
        var boardName = request.Type switch
        {
            "Singles" => PumbilitySingles,
            "Doubles" => PumbilityDoubles,
            _ => PumbilityAll
        };
        var pumbilityBoard = stats.RatingBoards.TryGetValue(boardName, out var board) ? board : null;

        var previous = await _snapshots.GetSealedBefore(request.Mix, latest.Id, cancellationToken);
        IReadOnlyList<OfficialRankingRecord> rankings;
        if (pumbilityBoard != null)
        {
            var previousPlaces = previous == null
                ? new Dictionary<int, int>()
                : (await GetSnapshotStats(request.Mix, previous.Id, cancellationToken))
                .RatingBoards.TryGetValue(boardName, out var prevBoard)
                    ? prevBoard.ToDictionary(p => p.PlayerId, p => p.Place)
                    : new Dictionary<int, int>();
            rankings = pumbilityBoard
                .OrderBy(p => p.Place)
                .Select(p => BuildRanking(p.Place,
                    previousPlaces.TryGetValue(p.PlayerId, out var prev) ? prev : (int?)null,
                    p.PlayerId, p.Score, stats))
                .ToArray();
        }
        else
        {
            var previousRanks = previous == null
                ? new Dictionary<int, int>()
                : ComputedRanks(await GetSnapshotStats(request.Mix, previous.Id, cancellationToken),
                    request.Type);
            var currentRanks = ComputedRanks(stats, request.Type);
            rankings = currentRanks
                .OrderBy(kv => kv.Value)
                .Select(kv => BuildRanking(kv.Value,
                    previousRanks.TryGetValue(kv.Key, out var prev) ? prev : (int?)null,
                    kv.Key, stats.ByPlayer[kv.Key].RatingFor(request.Type), stats))
                .ToArray();
        }

        var players = (await _snapshots.GetPlayersByIds(rankings.Select(r => r.Player.PlayerId).ToArray(),
                cancellationToken))
            .ToDictionary(p => p.Id);
        return new OfficialRankingsRecord(latest.CompletedAt, pumbilityBoard != null, rankings
            .Select(r => players.TryGetValue(r.Player.PlayerId, out var player)
                ? r with { Player = ToRecord(player) }
                : r)
            .ToArray());
    }

    private static OfficialRankingRecord BuildRanking(int rank, int? previousRank, int playerId, decimal rating,
        SnapshotStats stats)
    {
        var playerStats = stats.ByPlayer.TryGetValue(playerId, out var s) ? s : null;
        return new OfficialRankingRecord(rank, previousRank,
            new OfficialPlayerRecord(playerId, string.Empty, null, null), rating,
            playerStats?.BoardsInTop ?? 0, playerStats?.NumberOnes ?? 0, playerStats?.PlayerType);
    }

    private static Dictionary<int, int> ComputedRanks(SnapshotStats stats, string type)
    {
        var ranked = stats.ByPlayer.Values
            .Where(p => p.RatingFor(type) > 0)
            .OrderByDescending(p => p.RatingFor(type))
            .ToArray();
        var ranks = new Dictionary<int, int>(ranked.Length);
        for (var i = 0; i < ranked.Length; i++) ranks[ranked[i].PlayerId] = i + 1;
        return ranks;
    }

    public async Task<OfficialPlayerProfileRecord?> Handle(GetOfficialPlayerProfileQuery request,
        CancellationToken cancellationToken)
    {
        var player = await _snapshots.GetPlayerByUsername(request.Mix, request.Username, cancellationToken);
        if (player == null) return null;

        var latest = await _snapshots.GetLatestSealed(request.Mix, cancellationToken);
        if (latest?.CompletedAt == null) return null;

        var stats = await GetSnapshotStats(request.Mix, latest.Id, cancellationToken);
        var playerStats = stats.ByPlayer.TryGetValue(player.Id, out var s) ? s : null;

        var timeline = await _snapshots.GetPlayerTimeline(player.Id, cancellationToken);
        var history = timeline.GroupBy(r => (r.SnapshotId, r.CompletedAt))
            .OrderBy(g => g.Key.CompletedAt)
            .Select(g =>
            {
                var pumbility = g.FirstOrDefault(r =>
                    r.LeaderboardType == LeaderboardTypes.Rating && r.BoardName == PumbilityAll);
                return new OfficialPlayerHistoryPoint(g.Key.CompletedAt, pumbility?.Score,
                    pumbility?.Place, g.Count(r => r.LeaderboardType == LeaderboardTypes.Chart));
            })
            .ToArray();

        var previous = await _snapshots.GetSealedBefore(request.Mix, latest.Id, cancellationToken);
        var previousPlaces = new Dictionary<Guid, int>();
        var previousPumbilityRank = (int?)null;
        if (previous != null)
        {
            var previousRows = timeline.Where(r => r.SnapshotId == previous.Id).ToArray();
            previousPlaces = previousRows
                .Where(r => r.LeaderboardType == LeaderboardTypes.Chart && r.ChartId != null)
                .GroupBy(r => r.ChartId!.Value)
                .ToDictionary(g => g.Key, g => g.First().Place);
            previousPumbilityRank = previousRows.FirstOrDefault(r =>
                r.LeaderboardType == LeaderboardTypes.Rating && r.BoardName == PumbilityAll)?.Place;
        }

        var currentRows = timeline.Where(r => r.SnapshotId == latest.Id).ToArray();
        var pumbilityRow = currentRows.FirstOrDefault(r =>
            r.LeaderboardType == LeaderboardTypes.Rating && r.BoardName == PumbilityAll);
        var chartRows = currentRows.Where(r =>
                r.LeaderboardType == LeaderboardTypes.Chart && r.ChartId != null)
            .ToArray();
        var placements = chartRows
            .OrderBy(r => r.Place).ThenByDescending(r => r.Score)
            .Select(r => new OfficialPlayerChartRecord(r.ChartId!.Value, r.Place,
                previousPlaces.TryGetValue(r.ChartId.Value, out var prev) ? prev - r.Place : null,
                (int)r.Score, playerStats?.ChartRatings.TryGetValue(r.ChartId.Value, out var rating) == true
                    ? rating
                    : 0))
            .Concat(await SupplementFromLinkedScores(request.Mix, player, chartRows, cancellationToken))
            .ToArray();

        return new OfficialPlayerProfileRecord(ToRecord(player), playerStats?.PlayerType,
            pumbilityRow?.Score, pumbilityRow?.Place,
            pumbilityRow?.Place != null && previousPumbilityRank != null
                ? previousPumbilityRank - pumbilityRow.Place
                : null,
            chartRows.Length,
            chartRows.Count(r => r.Place == 1),
            chartRows.Length == 0 ? 0 : chartRows.Min(r => r.Place),
            chartRows.Count(r => r.Place <= 10),
            history, placements);
    }

    /// <summary>
    ///     A linked player whose boards can't fill a top 50 gets the balance from their own
    ///     piuscores bests — flagged Supplemented, no board place to show. Board rows stay
    ///     the source of truth for every stat tile; this only rounds out the chart list.
    /// </summary>
    private async Task<IEnumerable<OfficialPlayerChartRecord>> SupplementFromLinkedScores(MixEnum mix,
        PlayerDimension player, IReadOnlyCollection<PlayerTimelineRow> boardRows, CancellationToken ct)
    {
        if (player.UserId == null || boardRows.Count >= 50) return Array.Empty<OfficialPlayerChartRecord>();

        var charts = await GetChartLookup(mix, ct);
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);
        var onBoards = boardRows.Where(r => r.ChartId != null).Select(r => r.ChartId!.Value).ToHashSet();
        return (await _scores.GetBestScores(mix, player.UserId.Value, ct))
            .Where(s => !s.IsBroken && s.Score != null && !onBoards.Contains(s.ChartId) &&
                        charts.TryGetValue(s.ChartId, out var chart) &&
                        chart.Type is ChartType.Single or ChartType.Double)
            .Select(s =>
            {
                var chart = charts[s.ChartId];
                return new OfficialPlayerChartRecord(s.ChartId, null, null, (int)s.Score!.Value,
                    (int)scoring.GetScore(chart.Type, chart.Level, s.Score.Value), Supplemented: true);
            })
            .OrderByDescending(s => s.ComputedRating)
            .Take(50 - boardRows.Count);
    }

    private async Task<IReadOnlyDictionary<Guid, Chart>> GetChartLookup(MixEnum mix, CancellationToken ct)
    {
        return (await _cache.GetOrCreateAsync($"OfficialHubCharts__{mix}", async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(12);
            return (IReadOnlyDictionary<Guid, Chart>)(await _charts.GetCharts(mix, cancellationToken: ct))
                .ToDictionary(c => c.Id);
        }))!;
    }

    public async Task<IReadOnlyList<string>> Handle(GetOfficialPlayerNamesQuery request,
        CancellationToken cancellationToken)
    {
        return await _snapshots.GetPlayerNames(request.Mix, cancellationToken);
    }

    public async Task<IReadOnlyList<OfficialPopularityRecord>> Handle(GetOfficialPopularityQuery request,
        CancellationToken cancellationToken)
    {
        var history = await _snapshots.GetPopularityHistory(request.Mix, request.TrendSnapshots,
            cancellationToken);
        if (history.Count == 0) return Array.Empty<OfficialPopularityRecord>();

        // Rows arrive newest snapshot first; index 0 is the current board.
        var snapshotOrder = history.Select(h => h.SnapshotId).Distinct().ToArray();
        var current = history.Where(h => h.SnapshotId == snapshotOrder[0]).ToArray();
        var previous = snapshotOrder.Length > 1
            ? history.Where(h => h.SnapshotId == snapshotOrder[1])
                .ToDictionary(h => h.ChartId, h => h.Place)
            : new Dictionary<Guid, int>();
        var trend = history.GroupBy(h => h.ChartId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g
                .OrderByDescending(h => Array.IndexOf(snapshotOrder, h.SnapshotId))
                .Select(h => h.Place).ToArray());

        return current
            .OrderBy(c => c.Place)
            .Select(c => new OfficialPopularityRecord(c.ChartId, c.Place,
                previous.TryGetValue(c.ChartId, out var prev) ? prev : null,
                trend[c.ChartId]))
            .ToArray();
    }

    public async Task<IReadOnlyList<ImportRunRecord>> Handle(GetImportRunsQuery request,
        CancellationToken cancellationToken)
    {
        return (await _snapshots.GetRecentRuns(request.Mix, request.Take, cancellationToken))
            .Select(r => new ImportRunRecord(r.Id, r.StartedAt, r.CompletedAt, r.IsBaseline, r.Stage,
                r.BoardsExpected, r.BoardsWritten, r.BoardsSkipped, r.Error))
            .ToArray();
    }

    public async Task<WhatItTakesRecord> Handle(GetWhatItTakesQuery request, CancellationToken cancellationToken)
    {
        var latest = await _snapshots.GetLatestSealed(request.Mix, cancellationToken);
        if (latest?.CompletedAt == null)
            return new WhatItTakesRecord(null, false, 0, null, Array.Empty<CutlineTierRecord>(),
                Array.Empty<BoardCutlineRecord>(), Array.Empty<CutlineHistoryPointRecord>());

        return (await _cache.GetOrCreateAsync(
            $"OfficialWhatItTakes__{request.Mix}__{request.Type}__{latest.Id}", async entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromHours(12);
                return await BuildWhatItTakes(request.Mix, request.Type, latest, cancellationToken);
            }))!;
    }

    private async Task<WhatItTakesRecord> BuildWhatItTakes(MixEnum mix, string type, SnapshotRun latest,
        CancellationToken ct)
    {
        var boardName = type switch
        {
            "Singles" => PumbilitySingles,
            "Doubles" => PumbilityDoubles,
            _ => PumbilityAll
        };
        var chartType = type == "Doubles" ? ChartType.Double : ChartType.Single;
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);
        var stats = await GetSnapshotStats(mix, latest.Id, ct);
        var board = stats.RatingBoards.TryGetValue(boardName, out var rows)
            ? rows
            : Array.Empty<PlacementRow>();

        var previous = await _snapshots.GetSealedBefore(mix, latest.Id, ct);
        var previousBoard = previous == null
            ? Array.Empty<PlacementRow>()
            : (await GetSnapshotStats(mix, previous.Id, ct)).RatingBoards.TryGetValue(boardName, out var prev)
                ? prev
                : Array.Empty<PlacementRow>();

        CutlineTierRecord Tier(int rank, decimal value)
        {
            var previousValue = CutlineCalculator.ValueAtRank(previousBoard, rank);
            return new CutlineTierRecord(rank, value, value - previousValue,
                CutlineCalculator.LevelFor(scoring, chartType, PhoenixLetterGrade.AAA, value),
                CutlineCalculator.LevelFor(scoring, chartType, PhoenixLetterGrade.S, value),
                CutlineCalculator.LevelFor(scoring, chartType, PhoenixLetterGrade.SS, value),
                CutlineCalculator.LevelFor(scoring, chartType, PhoenixLetterGrade.SSS, value));
        }

        var tiers = CutlineCalculator.TierLadder
            .Select(rank => (Rank: rank, Value: CutlineCalculator.ValueAtRank(board, rank)))
            .Where(t => t.Value != null)
            .Select(t => Tier(t.Rank, t.Value!.Value))
            .ToArray();
        var boardFull = board.Count >= CutlineCalculator.BoardCapacity;
        var entry = boardFull ? tiers.FirstOrDefault(t => t.Rank == CutlineCalculator.BoardCapacity) : null;

        var comparisons = new List<BoardCutlineRecord>();
        foreach (var (name, label) in new[]
                 {
                     (PumbilityAll, "All"), (PumbilitySingles, "Singles"), (PumbilityDoubles, "Doubles")
                 })
        {
            var compareBoard = stats.RatingBoards.TryGetValue(name, out var b)
                ? b
                : Array.Empty<PlacementRow>();
            var compareFull = compareBoard.Count >= CutlineCalculator.BoardCapacity;
            var value = compareFull
                ? CutlineCalculator.ValueAtRank(compareBoard, CutlineCalculator.BoardCapacity)
                : null;
            var previousValue = CutlineCalculator.ValueAtRank(previous == null
                    ? Array.Empty<PlacementRow>()
                    : (await GetSnapshotStats(mix, previous.Id, ct)).RatingBoards.TryGetValue(name, out var pb)
                        ? pb
                        : Array.Empty<PlacementRow>(),
                CutlineCalculator.BoardCapacity);
            comparisons.Add(new BoardCutlineRecord(label, value, value - previousValue, compareFull));
        }

        var history = (await _snapshots.GetBoardFloorHistory(mix, boardName, ct))
            .Where(h => h.Count >= CutlineCalculator.BoardCapacity)
            .Select(h => new CutlineHistoryPointRecord(h.CompletedAt, h.MinScore,
                CutlineCalculator.LevelFor(scoring, chartType, PhoenixLetterGrade.AAA, h.MinScore),
                CutlineCalculator.LevelFor(scoring, chartType, PhoenixLetterGrade.S, h.MinScore),
                CutlineCalculator.LevelFor(scoring, chartType, PhoenixLetterGrade.SS, h.MinScore),
                CutlineCalculator.LevelFor(scoring, chartType, PhoenixLetterGrade.SSS, h.MinScore)))
            .ToArray();

        return new WhatItTakesRecord(latest.CompletedAt, boardFull, board.Count, entry, tiers, comparisons,
            history);
    }

    private static OfficialPlayerRecord ToRecord(PlayerDimension player)
    {
        return new OfficialPlayerRecord(player.Id, player.Username, player.Avatar, player.UserId);
    }

    // ── snapshot-scoped stats ────────────────────────────────────────────────

    private sealed record PlayerSnapshotStats(int PlayerId, int BoardsInTop, int NumberOnes, int RatingAll,
        int RatingSingles, int RatingDoubles, RecapPlayerType? PlayerType,
        IReadOnlyDictionary<Guid, int> ChartRatings)
    {
        public decimal RatingFor(string type)
        {
            return type switch
            {
                "Singles" => RatingSingles,
                "Doubles" => RatingDoubles,
                _ => RatingAll
            };
        }
    }

    private sealed record SnapshotStats(
        IReadOnlyDictionary<int, PlayerSnapshotStats> ByPlayer,
        IReadOnlyDictionary<string, IReadOnlyList<PlacementRow>> RatingBoards);

    private async Task<SnapshotStats> GetSnapshotStats(MixEnum mix, int snapshotId, CancellationToken ct)
    {
        return (await _cache.GetOrCreateAsync($"OfficialSnapshotStats__{mix}__{snapshotId}", async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(12);
            var details = await _snapshots.GetPlacementDetails(snapshotId, ct);
            return ComputeStats(mix, details);
        }))!;
    }

    private static SnapshotStats ComputeStats(MixEnum mix, IReadOnlyList<PlacementDetail> details)
    {
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);
        var ratingBoards = details
            .Where(d => d.LeaderboardType == LeaderboardTypes.Rating)
            .GroupBy(d => d.BoardName)
            .ToDictionary(g => g.Key,
                g => (IReadOnlyList<PlacementRow>)g
                    .Select(d => new PlacementRow(d.LeaderboardId, d.PlayerId, d.Place, d.Score))
                    .OrderBy(p => p.Place).ToArray());

        var byPlayer = new Dictionary<int, PlayerSnapshotStats>();
        foreach (var group in details
                     .Where(d => d.LeaderboardType == LeaderboardTypes.Chart &&
                                 d.ChartType != ChartType.CoOp.ToString() && d.Level != null)
                     .GroupBy(d => d.PlayerId))
        {
            var contributions = group
                .Select(d => (Detail: d,
                    Rating: (int)scoring.GetScore(DifficultyLevel.From(d.Level!.Value),
                        PhoenixScore.From((int)d.Score))))
                .ToArray();
            var top50 = contributions.OrderByDescending(c => c.Rating).Take(50).ToArray();
            var singles = contributions.Where(c => c.Detail.ChartType == ChartType.Single.ToString())
                .OrderByDescending(c => c.Rating).Take(50).Sum(c => c.Rating);
            var doubles = contributions.Where(c => c.Detail.ChartType == ChartType.Double.ToString())
                .OrderByDescending(c => c.Rating).Take(50).Sum(c => c.Rating);
            var playerType = RecapPlayerTypeCalculator.Calculate(
                top50.Select(c => PhoenixScore.From((int)c.Detail.Score)).ToArray());
            byPlayer[group.Key] = new PlayerSnapshotStats(group.Key,
                contributions.Length,
                contributions.Count(c => c.Detail.Place == 1),
                top50.Sum(c => c.Rating), singles, doubles, playerType,
                contributions.Where(c => c.Detail.ChartId != null)
                    .GroupBy(c => c.Detail.ChartId!.Value)
                    .ToDictionary(g => g.Key, g => g.Max(c => c.Rating)));
        }

        return new SnapshotStats(byPlayer, ratingBoards);
    }
}
