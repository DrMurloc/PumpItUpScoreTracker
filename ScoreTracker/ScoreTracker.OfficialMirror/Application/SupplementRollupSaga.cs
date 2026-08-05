using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Events;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     Builds the supplemented reading of a sealed snapshot: the ledger bests of linked public
///     players, merged into every board they belong on.
///     <para>
///         It is deliberately not a stage of <see cref="LeaderboardSweepSaga" />. The sweep
///         spends forty minutes talking to a remote site and its seal is the thing that makes a
///         week visible; hanging a local roll-up off the end of it would mean a failure in our
///         own data could keep piugame's from ever appearing. Here, a roll-up that dies leaves
///         the official week intact and the previous supplemented one still standing.
///     </para>
///     <para>
///         Two triggers, one path: the sweep's seal event for the weekly cadence, and an admin
///         command for on-demand. Both re-run cleanly, because the first thing a run does is
///         delete its own previous output.
///     </para>
/// </summary>
internal sealed class SupplementRollupSaga : IConsumer<RollUpSupplementedLeaderboardsCommand>,
    IConsumer<OfficialSnapshotSealedEvent>
{
    /// <summary>
    ///     Players per ledger read. The whole Phoenix cohort at once is roughly six hundred
    ///     thousand rows and a parameter list SQL Server would refuse.
    /// </summary>
    private const int UserChunk = 200;

    private readonly IOfficialSnapshotRepository _snapshots;
    private readonly IOfficialRecordRepository _records;
    private readonly IScoreReader _scores;
    private readonly IPlayerStatsReader _playerStats;
    private readonly IUserReader _users;
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;

    public SupplementRollupSaga(IOfficialSnapshotRepository snapshots, IOfficialRecordRepository records,
        IScoreReader scores, IPlayerStatsReader playerStats, IUserReader users, IMemoryCache cache,
        ILogger<SupplementRollupSaga> logger)
    {
        _snapshots = snapshots;
        _records = records;
        _scores = scores;
        _playerStats = playerStats;
        _users = users;
        _cache = cache;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<RollUpSupplementedLeaderboardsCommand> context) =>
        RollUp(context.Message.Mix, context.CancellationToken);

    public Task Consume(ConsumeContext<OfficialSnapshotSealedEvent> context) =>
        RollUp(context.Message.Mix, context.CancellationToken);

    private async Task RollUp(MixEnum mix, CancellationToken ct)
    {
        var latest = await _snapshots.GetLatestSealed(mix, ct);
        if (latest == null)
        {
            _logger.LogInformation("No sealed {Mix} snapshot to supplement yet", mix);
            return;
        }

        var cohort = await Cohort(mix, ct);
        if (cohort.Count == 0)
        {
            _logger.LogInformation("No linked public {Mix} players to supplement with", mix);
            return;
        }

        // The supplemented series has its own week one, which is a different question from
        // whether the official sweep was a baseline. Without this the first roll-up announces
        // several hundred simultaneous debuts and a board-wide flood of "new entries".
        var isBaseline = !await _snapshots.AnySupplemented(mix, ct);

        // Clear this run's own previous output first, so a re-press replaces rather than doubles.
        await _snapshots.DeleteSupplementedPlacements(latest.Id, ct);
        await _records.DeleteSupplementedHighlights(latest.Id, ct);

        var boards = await _snapshots.GetBoards(mix, ct);
        var written = new List<PlacementRow>();
        written.AddRange(await ChartBoardRows(latest.Id, mix, boards, cohort, ct));
        written.AddRange(await RatingBoardRows(latest.Id, mix, boards, cohort, ct));

        await _snapshots.WritePlacements(latest.Id, written, ct);
        await ComputeHighlights(latest.Id, mix, boards, isBaseline, ct);
        EvictSnapshotCaches(mix, latest.Id);

        _logger.LogInformation(
            "{Mix} snapshot {SnapshotId}: {Rows} supplemented rows from {Players} linked public players",
            mix, latest.Id, written.Count, cohort.Count);
    }

    /// <summary>
    ///     This Week, read the supplemented way. The same calculator over the merged board, with
    ///     the record-book kinds switched off — every diff-based kind (movers, climbers, pulse,
    ///     gainers, floors, debuts) answers differently once our players are on the board, while
    ///     world firsts and new #1s stay a fact about what piugame published.
    ///     <para>
    ///         Both the debut seen-set and the previous week are read at the same scope as the
    ///         current one. Mixing scopes would make every supplemented player debut every week,
    ///         because they were never in the official history being compared against.
    ///     </para>
    /// </summary>
    private async Task ComputeHighlights(int snapshotId, MixEnum mix, IReadOnlyList<BoardDimension> boards,
        bool isBaseline, CancellationToken ct)
    {
        const PlacementScope scope = PlacementScope.IncludingSupplemented;
        var previous = await _snapshots.GetSealedBefore(mix, snapshotId, ct);

        var input = new HighlightsInput(mix, snapshotId, isBaseline, boards,
            SupplementMerge.MergedBoards(await _snapshots.GetPlacements(snapshotId, scope, ct)),
            previous == null
                ? null
                : SupplementMerge.MergedBoards(await _snapshots.GetPlacements(previous.Id, scope, ct)),
            Array.Empty<BoardRecordRow>(),
            Array.Empty<FolderRecordRow>(),
            CrossMixRecordHighs.Empty,
            await _snapshots.GetSeenPlayerIds(mix, snapshotId, scope, ct),
            ScoringConfiguration.PumbilityScoring(mix, false),
            false);

        var result = HighlightsCalculator.Calculate(input);
        await _records.WriteHighlights(snapshotId, mix, result.Highlights, true, ct);
        _logger.LogInformation("{Mix} snapshot {SnapshotId}: {Count} supplemented highlights", mix, snapshotId,
            result.Highlights.Count);
    }

    /// <summary>
    ///     The linked public players, as mirror-player id to site user id. Identity is resolved
    ///     first and visibility second: a tag whose most recent claimant has gone private
    ///     produces no row at all, rather than falling back to whoever held it before — those
    ///     are different people's scores.
    ///     <para>
    ///         **One account can own several tags in a mix.** `LinkPlayer` sets `UserId` on the
    ///         tag an import proved and never clears it from the one before, so a rename or a
    ///         second game card leaves an account linked to both rows permanently. Their ledger
    ///         is per user and per mix, not per card, so publishing it under both tags would put
    ///         one human on every board twice. The most recently seen tag wins — `LastSeenAt` is
    ///         refreshed every sweep for a tag still appearing on a board, which makes it the
    ///         mirror's own evidence of which one is live — and the newest row breaks a tie.
    ///     </para>
    /// </summary>
    private async Task<IReadOnlyDictionary<int, Guid>> Cohort(MixEnum mix, CancellationToken ct)
    {
        var linked = (await _snapshots.GetPlayers(mix, ct))
            .Where(p => p.UserId != null)
            .ToArray();
        if (linked.Length == 0) return new Dictionary<int, Guid>();

        var publicIds = (await _users.GetUsers(linked.Select(p => p.UserId!.Value).Distinct(), ct))
            .Where(u => u.IsPublic)
            .Select(u => u.Id)
            .ToHashSet();

        return linked
            .Where(p => publicIds.Contains(p.UserId!.Value))
            .GroupBy(p => p.UserId!.Value)
            .ToDictionary(
                g => g.OrderByDescending(p => p.LastSeenAt).ThenByDescending(p => p.Id).First().Id,
                g => g.Key);
    }

    private async Task<IReadOnlyList<PlacementRow>> ChartBoardRows(int snapshotId, MixEnum mix,
        IReadOnlyList<BoardDimension> boards, IReadOnlyDictionary<int, Guid> cohort, CancellationToken ct)
    {
        // A chart can carry more than one board row: the dimension is unique on NAME, so a song
        // renamed on piugame gets a fresh row pointing at the same chart while the old one
        // lingers. The newest row is the live one.
        var chartBoards = boards
            .Where(b => b.LeaderboardType == LeaderboardTypes.Chart && b.ChartId != null)
            .GroupBy(b => b.ChartId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.Id).First());
        if (chartBoards.Count == 0) return Array.Empty<PlacementRow>();

        // Safe to invert: Cohort has already collapsed each account to a single tag.
        var playerByUser = cohort.ToDictionary(kv => kv.Value, kv => kv.Key);

        // Chart id to the cohort's bests on it. Only charts that actually have a mirrored
        // board are kept: a score on a chart piugame publishes no board for has nowhere to go.
        var byChart = new Dictionary<Guid, List<(int PlayerId, decimal Score)>>();
        foreach (var chunk in cohort.Values.Distinct().Chunk(UserChunk))
        foreach (var (userId, record) in await _scores.GetVerifiedBests(mix, chunk, ct))
        {
            if (record.Score is not { } score) continue;
            if (!chartBoards.ContainsKey(record.ChartId)) continue;
            if (!playerByUser.TryGetValue(userId, out var playerId)) continue;

            if (!byChart.TryGetValue(record.ChartId, out var rows))
                byChart[record.ChartId] = rows = new List<(int, decimal)>();
            rows.Add((playerId, score));
        }

        var boardIds = byChart.Keys.Select(c => chartBoards[c].Id).ToArray();
        var official = (await _snapshots.GetBoardPlacements(snapshotId, boardIds, PlacementScope.OfficialOnly, ct))
            .ToLookup(p => p.LeaderboardId);

        var results = new List<PlacementRow>();
        var shortBoards = 0;
        foreach (var (chartId, ledger) in byChart)
        {
            var board = chartBoards[chartId];
            var officialRows = official[board.Id].ToArray();
            var stored = SupplementMerge.RowsToStore(board.Id, officialRows, ledger);
            results.AddRange(stored);

            if (SupplementMerge.RowsAboveOfficialTail(
                    SupplementMerge.MergedBoard(officialRows.Concat(stored))) > 0) shortBoards++;
        }

        // A supplemented row above the official tail can only mean the official board was
        // short this week — a skipped fetch, or a chart the mirror holds no board for. Said
        // out loud, because the alternative is a board that quietly looks wrong.
        if (shortBoards > 0)
            _logger.LogInformation(
                "{Mix} snapshot {SnapshotId}: {Count} chart boards had supplemented rows above the official tail",
                mix, snapshotId, shortBoards);

        return results;
    }

    /// <summary>
    ///     Only the PUMBILITY boards. Phoenix's per-level rating lists are piugame's own
    ///     ranking of a folder and we compute no equivalent, so there is nothing honest to
    ///     merge into them.
    /// </summary>
    private async Task<IReadOnlyList<PlacementRow>> RatingBoardRows(int snapshotId, MixEnum mix,
        IReadOnlyList<BoardDimension> boards, IReadOnlyDictionary<int, Guid> cohort, CancellationToken ct)
    {
        var stats = new Dictionary<Guid, PlayerRatings>();
        foreach (var chunk in cohort.Values.Distinct().Chunk(UserChunk))
        foreach (var s in await _playerStats.GetStats(mix, chunk, ct))
            stats[s.UserId] = new PlayerRatings(s.SkillRating, s.SinglesRating, s.DoublesRating);

        var results = new List<PlacementRow>();
        foreach (var (name, pick) in RatingBoardValues)
        {
            var board = boards.FirstOrDefault(b =>
                b.LeaderboardType == LeaderboardTypes.Rating && b.Name == name);
            if (board == null) continue;

            var ledger = cohort
                .Select(kv => (PlayerId: kv.Key, Score: (decimal)(stats.TryGetValue(kv.Value, out var s)
                    ? pick(s)
                    : 0)))
                .Where(r => r.Score > 0)
                .ToArray();
            if (ledger.Length == 0) continue;

            var official = await _snapshots.GetBoardPlacements(snapshotId, board.Id, PlacementScope.OfficialOnly, ct);
            results.AddRange(SupplementMerge.RowsToStore(board.Id, official, ledger));
        }

        return results;
    }

    private static readonly (string Name, Func<PlayerRatings, int> Pick)[] RatingBoardValues =
    {
        (PumbilityBoards.Combined, r => r.Skill),
        (PumbilityBoards.Singles, r => r.Singles),
        (PumbilityBoards.Doubles, r => r.Doubles)
    };

    /// <summary>The three pool sums a PUMBILITY board can rank on.</summary>
    private readonly record struct PlayerRatings(int Skill, int Singles, int Doubles);

    /// <summary>
    ///     A sealed snapshot is normally immutable, which is what makes keying a cache on its
    ///     id safe. This run breaks that assumption on purpose by adding rows to one, so it
    ///     evicts both readings by hand rather than waiting out a sliding expiry.
    /// </summary>
    private void EvictSnapshotCaches(MixEnum mix, int snapshotId)
    {
        foreach (var supplemented in OfficialCacheKeys.Readings)
            _cache.Remove(OfficialCacheKeys.SnapshotStats(mix, snapshotId, supplemented));
        foreach (var type in OfficialCacheKeys.WhatItTakesTypes)
            _cache.Remove(OfficialCacheKeys.WhatItTakes(mix, type, snapshotId));
    }
}
