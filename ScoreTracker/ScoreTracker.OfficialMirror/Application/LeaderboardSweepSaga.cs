using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Events;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     The weekly leaderboard sweep: one run per trigger writes one snapshot — rating
///     boards, chart boards (checkpointed per board), popularity, the two tier-list feeds —
///     and seals it. Only the seal makes the snapshot visible; a failure leaves the header
///     carrying the stage and error while the site keeps serving the last sealed snapshot.
/// </summary>
internal sealed class LeaderboardSweepSaga : IConsumer<StartLeaderboardImportCommand>,
    IConsumer<RebuildWeeklyHighlightsCommand>,
    IConsumer<RefreshPopularityCommand>,
    IConsumer<SeedBaselineSnapshotCommand>,
    IRequestHandler<GetLastLeaderboardImportTimestampQuery, DateTimeOffset?>,
    IRequestHandler<GetMissingChartsQuery, IReadOnlyList<MissingChartRecord>>,
    IRequestHandler<ResolveMissingChartCommand>
{
    private static readonly TimeSpan UnsealedPurgeAge = TimeSpan.FromDays(7);
    // The sweep checkpoints at least once per board, so a live run heartbeats every few
    // seconds; a run that has been silent this long is dead (killed process, deploy) and
    // must not hold the overlap lock.
    private static readonly TimeSpan HeartbeatWindow = TimeSpan.FromMinutes(15);

    private readonly IOfficialSiteClient _officialSite;
    private readonly IOfficialSnapshotRepository _snapshots;
    private readonly IOfficialRecordRepository _records;
    private readonly IOfficialPlayerIdentityRepository _identity;
    private readonly IOfficialLeaderboardRepository _legacy;
    private readonly IChartRepository _charts;
    private readonly ITierListRepository _tierLists;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IBus _bus;
    private readonly ILogger _logger;

    public LeaderboardSweepSaga(IOfficialSiteClient officialSite, IOfficialSnapshotRepository snapshots,
        IOfficialRecordRepository records, IOfficialPlayerIdentityRepository identity,
        IOfficialLeaderboardRepository legacy, IChartRepository charts,
        ITierListRepository tierLists, IDateTimeOffsetAccessor dateTime,
        IBus bus, ILogger<LeaderboardSweepSaga> logger)
    {
        _officialSite = officialSite;
        _snapshots = snapshots;
        _records = records;
        _identity = identity;
        _legacy = legacy;
        _charts = charts;
        _tierLists = tierLists;
        _dateTime = dateTime;
        _bus = bus;
        _logger = logger;
    }

    public async Task<DateTimeOffset?> Handle(GetLastLeaderboardImportTimestampQuery request,
        CancellationToken cancellationToken)
    {
        return (await _snapshots.GetLatestSealed(request.Mix, cancellationToken))?.CompletedAt;
    }

    public async Task<IReadOnlyList<MissingChartRecord>> Handle(GetMissingChartsQuery request,
        CancellationToken cancellationToken)
    {
        return (await _snapshots.GetMissingCharts(request.Mix, cancellationToken))
            .Select(m => new MissingChartRecord(m.Id, m.SongName, m.ChartType, m.Level, m.FirstIdentified,
                m.LastIdentified))
            .ToArray();
    }

    public Task Handle(ResolveMissingChartCommand request, CancellationToken cancellationToken)
    {
        return _snapshots.DeleteMissingChart(request.Id, cancellationToken);
    }

    public async Task Consume(ConsumeContext<StartLeaderboardImportCommand> context)
    {
        var mix = context.Message.Mix;
        var ct = context.CancellationToken;
        var now = _dateTime.Now;

        await _snapshots.PurgeUnsealed(mix, now - UnsealedPurgeAge, ct);
        if (await _snapshots.HasLiveRun(mix, now - HeartbeatWindow, ct))
        {
            _logger.LogWarning(
                "A {Mix} leaderboard sweep is already in flight (heartbeat within {Window}); skipping this trigger",
                mix, HeartbeatWindow);
            return;
        }

        // A mix with no sealed snapshot yet baselines: records prime, highlights stay
        // silent, so the first week is data rather than three thousand celebrations.
        var isBaseline = !await _snapshots.AnySealed(mix, ct);
        var snapshotId = await _snapshots.CreateRun(mix, isBaseline, now, ct);
        try
        {
            await RunSweep(snapshotId, mix, isBaseline, ct);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Mix} leaderboard sweep failed; snapshot {SnapshotId} stays unsealed", mix,
                snapshotId);
            await _snapshots.MarkFailed(snapshotId, e.ToString(), CancellationToken.None);
        }
    }

    private async Task RunSweep(int snapshotId, MixEnum mix, bool isBaseline, CancellationToken ct)
    {
        var players = new SweepPlayerCache(_snapshots, mix, _dateTime.Now);

        await _snapshots.UpdateProgress(snapshotId, "RatingBoards", 0, 0, 0, _dateTime.Now, ct);
        await SweepRatingBoards(snapshotId, mix, players, ct);

        var chartScores = await SweepChartBoards(snapshotId, mix, players, ct);

        await _snapshots.UpdateProgress(snapshotId, "Popularity", 0, 0, 0, _dateTime.Now, ct);
        await SweepPopularity(snapshotId, mix, ct);

        await _snapshots.UpdateProgress(snapshotId, "TierLists", 0, 0, 0, _dateTime.Now, ct);
        await PopulateOfficialScoresTierList(mix, chartScores, ct);

        await _snapshots.UpdateProgress(snapshotId, "Highlights", 0, 0, 0, _dateTime.Now, ct);
        await ComputeHighlights(snapshotId, mix, isBaseline, ct);

        await _snapshots.Seal(snapshotId, _dateTime.Now, ct);
        _logger.LogInformation("{Mix} snapshot {SnapshotId} sealed", mix, snapshotId);

        // The digest feed reads the sealed week's highlights + cutlines; it skips baseline seals.
        await _bus.Publish(new OfficialSnapshotSealedEvent(mix, isBaseline), ct);
    }

    private async Task ComputeHighlights(int snapshotId, MixEnum mix, bool isBaseline, CancellationToken ct)
    {
        var previous = await _snapshots.GetSealedBefore(mix, snapshotId, ct);
        var input = new HighlightsInput(snapshotId, isBaseline,
            await _snapshots.GetBoards(mix, ct),
            await _snapshots.GetPlacements(snapshotId, ct),
            previous == null ? null : await _snapshots.GetPlacements(previous.Id, ct),
            await _records.GetBoardRecords(mix, ct),
            await _records.GetFolderRecords(mix, ct));
        var result = HighlightsCalculator.Calculate(input);
        await _records.WriteHighlights(snapshotId, mix, result.Highlights, ct);
        await _records.UpsertBoardRecords(result.UpdatedBoardRecords, ct);
        await _records.UpsertFolderRecords(mix, result.UpdatedFolderRecords, ct);
        _logger.LogInformation("{Mix} snapshot {SnapshotId}: {Count} highlights", mix, snapshotId,
            result.Highlights.Count);

        if (!input.IsBaseline && input.Previous != null)
        {
            var proposals = RenameProposalDetector.Detect(snapshotId,
                await _snapshots.GetPlayers(mix, ct), input.Boards, input.Current, input.Previous);
            await _identity.WriteProposals(mix, proposals, ct);
            if (proposals.Count > 0)
                _logger.LogInformation("{Mix} snapshot {SnapshotId}: {Count} rename proposals", mix, snapshotId,
                    proposals.Count);
        }
    }

    /// <summary>
    ///     The cutover press: turns the legacy clear-and-rewrite table into a sealed
    ///     baseline snapshot — dims created (chart boards matched to the catalog by their
    ///     display name), placements copied verbatim, record books primed, zero highlights.
    ///     Refuses to run once any sealed snapshot exists: seeding after real sweeps would
    ///     fabricate a newest week out of stale data.
    /// </summary>
    public async Task Consume(ConsumeContext<SeedBaselineSnapshotCommand> context)
    {
        var mix = context.Message.Mix;
        var ct = context.CancellationToken;
        if (await _snapshots.AnySealed(mix, ct))
        {
            _logger.LogWarning("{Mix} already has sealed snapshots; baseline seed skipped", mix);
            return;
        }

        var legacy = (await _legacy.GetAllEntries(mix, ct)).ToArray();
        if (legacy.Length == 0)
        {
            _logger.LogWarning("{Mix} has no legacy leaderboard rows; nothing to seed", mix);
            return;
        }

        var snapshotId = await _snapshots.CreateRun(mix, true, _dateTime.Now, ct);
        try
        {
            var chartsByBoardName = (await _charts.GetCharts(mix, cancellationToken: ct))
                .GroupBy(c => c.Song.Name + " " + c.DifficultyString, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var players = new SweepPlayerCache(_snapshots, mix, _dateTime.Now);
            var written = 0;
            foreach (var boardGroup in legacy.GroupBy(e => (e.OfficialLeaderboardType, e.LeaderboardName)))
            {
                var chart = boardGroup.Key.OfficialLeaderboardType == LeaderboardTypes.Chart &&
                            chartsByBoardName.TryGetValue(boardGroup.Key.LeaderboardName, out var match)
                    ? match
                    : null;
                var board = await _snapshots.EnsureBoard(mix, boardGroup.Key.OfficialLeaderboardType,
                    boardGroup.Key.LeaderboardName, chart?.Id, chart?.Type.ToString(),
                    chart == null ? null : (int)chart.Level, ct);
                var entries = boardGroup.GroupBy(e => e.Username).Select(g => g.First()).ToArray();
                var ids = await players.Resolve(entries.Select(e => (e.Username, (Uri?)null)).ToArray(), ct);
                await _snapshots.WritePlacements(snapshotId,
                    entries.Select(e => new PlacementRow(board.Id, ids[e.Username], e.Place, e.Score))
                        .ToArray(), ct);
                written++;
                await _snapshots.UpdateProgress(snapshotId, "BaselineSeed", 0, written, 0, _dateTime.Now, ct);
            }

            await ComputeHighlights(snapshotId, mix, true, ct);
            await _snapshots.Seal(snapshotId, _dateTime.Now, ct);
            _logger.LogInformation("{Mix} baseline snapshot {SnapshotId} seeded from {Rows} legacy rows", mix,
                snapshotId, legacy.Length);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Mix} baseline seed failed; snapshot {SnapshotId} stays unsealed", mix,
                snapshotId);
            await _snapshots.MarkFailed(snapshotId, e.ToString(), CancellationToken.None);
        }
    }

    /// <summary>
    ///     Re-scrapes the play ranking alone and re-attaches it to the latest sealed
    ///     snapshot — the cheap repair/refresh when a full board sweep isn't warranted.
    /// </summary>
    public async Task Consume(ConsumeContext<RefreshPopularityCommand> context)
    {
        var mix = context.Message.Mix;
        var ct = context.CancellationToken;
        var latest = await _snapshots.GetLatestSealed(mix, ct);
        if (latest == null)
        {
            _logger.LogWarning("No sealed {Mix} snapshot to attach popularity to; run an import first", mix);
            return;
        }

        await _snapshots.DeletePopularity(latest.Id, ct);
        await SweepPopularity(latest.Id, mix, ct);
        _logger.LogInformation("{Mix} popularity refreshed onto snapshot {SnapshotId}", mix, latest.Id);
    }

    /// <summary>
    ///     Replays every sealed snapshot in order against reset record books — the admin
    ///     press for a highlight-rule change. The first replayed snapshot always runs
    ///     baseline-silent (there is nothing to diff against), matching the live path.
    /// </summary>
    public async Task Consume(ConsumeContext<RebuildWeeklyHighlightsCommand> context)
    {
        var mix = context.Message.Mix;
        var ct = context.CancellationToken;
        await _records.DeleteHighlights(mix, ct);
        await _records.ResetRecords(mix, ct);

        var runs = await _snapshots.GetSealedAscending(mix, ct);
        var boards = await _snapshots.GetBoards(mix, ct);
        IReadOnlyList<PlacementRow>? previous = null;
        var isFirst = true;
        foreach (var run in runs)
        {
            var current = await _snapshots.GetPlacements(run.Id, ct);
            var input = new HighlightsInput(run.Id, isFirst || run.IsBaseline, boards, current, previous,
                await _records.GetBoardRecords(mix, ct), await _records.GetFolderRecords(mix, ct));
            var result = HighlightsCalculator.Calculate(input);
            await _records.WriteHighlights(run.Id, mix, result.Highlights, ct);
            await _records.UpsertBoardRecords(result.UpdatedBoardRecords, ct);
            await _records.UpsertFolderRecords(mix, result.UpdatedFolderRecords, ct);
            previous = current;
            isFirst = false;
        }

        _logger.LogInformation("{Mix} weekly highlights rebuilt across {Count} snapshots", mix, runs.Count);
    }

    private async Task SweepRatingBoards(int snapshotId, MixEnum mix, SweepPlayerCache players,
        CancellationToken ct)
    {
        var entries = (await _officialSite.GetRatingBoards(mix, ct)).ToArray();
        foreach (var boardGroup in entries.GroupBy(e => e.BoardName))
        {
            var board = await _snapshots.EnsureBoard(mix, LeaderboardTypes.Rating, boardGroup.Key, null, null,
                null, ct);
            var ids = await players.Resolve(boardGroup.Select(e => (e.Username, (Uri?)null)).ToArray(), ct);
            var rows = Placements.Olympic(boardGroup, e => e.Value)
                .Select(p => new PlacementRow(board.Id, ids[p.Item.Username], p.Place, p.Item.Value))
                .ToArray();
            await _snapshots.WritePlacements(snapshotId, rows, ct);
        }
    }

    private async Task<List<(Chart Chart, string Username, PhoenixScore Score)>> SweepChartBoards(int snapshotId,
        MixEnum mix, SweepPlayerCache players, CancellationToken ct)
    {
        var chartScores = new List<(Chart Chart, string Username, PhoenixScore Score)>();
        var misses = new List<MissingChartSighting>();
        var written = 0;
        var skipped = 0;
        await foreach (var board in _officialSite.GetOfficialChartBoards(mix, ct))
        {
            if (board.Chart == null || board.SkipReason != null)
            {
                skipped++;
                if (board.Missing != null) misses.Add(board.Missing);
                _logger.LogWarning("Skipping board {Index}/{Total}: {Reason}", board.BoardIndex,
                    board.BoardsTotal, board.SkipReason);
                await _snapshots.UpdateProgress(snapshotId, "ChartBoards", board.BoardsTotal, written, skipped,
                    _dateTime.Now, ct);
                continue;
            }

            var chart = board.Chart;
            var dim = await _snapshots.EnsureBoard(mix, LeaderboardTypes.Chart,
                chart.Song.Name + " " + chart.DifficultyString, chart.Id, chart.Type.ToString(),
                chart.Level, ct);
            var ids = await players.Resolve(
                board.Entries.Select(e => (e.Username, (Uri?)e.AvatarUrl)).ToArray(), ct);
            var rows = Placements.Olympic(board.Entries, e => (int)e.Score)
                .Select(p => new PlacementRow(dim.Id, ids[p.Item.Username], p.Place, (int)p.Item.Score))
                .ToArray();
            await _snapshots.WritePlacements(snapshotId, rows, ct);
            chartScores.AddRange(board.Entries.Select(e => (chart, e.Username, e.Score)));

            written++;
            await _snapshots.UpdateProgress(snapshotId, "ChartBoards", board.BoardsTotal, written, skipped,
                _dateTime.Now, ct);
        }

        await _snapshots.UpsertMissingCharts(mix, misses, _dateTime.Now, ct);
        return chartScores;
    }

    private async Task SweepPopularity(int snapshotId, MixEnum mix, CancellationToken ct)
    {
        var (popularity, missing) = await _officialSite.GetOfficialChartLeaderboardEntries(mix, ct);
        var entries = popularity.ToArray();
        await _snapshots.UpsertMissingCharts(mix, missing, _dateTime.Now, ct);
        // Place -1 is the site's "not on the ranking" sentinel — categorized below but
        // never stored as a popularity placement.
        await _snapshots.WritePopularity(snapshotId,
            entries.Where(e => e.Place > 0).Select(e => (e.Chart.Id, e.Place)).ToArray(), ct);

        foreach (var levelTypeGroup in entries.GroupBy(e => (e.Chart.Level, e.Chart.Type)))
        {
            var charts = levelTypeGroup.ToArray();
            var average = charts.Average(c => c.Place);
            var standardDev = StdDev(charts.Select(c => c.Place), true);
            var mediumMin = average - standardDev / 2;
            var easyMin = average + standardDev / 2;
            var veryEasyMin = average + standardDev;
            var oneLevelOverrated = average + standardDev * 1.5;
            var hardMin = average - standardDev;
            var veryHardMin = average - standardDev * 1.5;
            foreach (var (chart, place, _) in levelTypeGroup)
            {
                var category = TierListCategory.Unrecorded;
                if (place == -1)
                    category = TierListCategory.Unrecorded;
                else if (place < veryHardMin)
                    category = TierListCategory.Overrated;
                else if (place < hardMin)
                    category = TierListCategory.VeryEasy;
                else if (place < mediumMin)
                    category = TierListCategory.Easy;
                else if (place < easyMin)
                    category = TierListCategory.Medium;
                else if (place < veryEasyMin)
                    category = TierListCategory.Hard;
                else if (place < oneLevelOverrated)
                    category = TierListCategory.VeryHard;
                else
                    category = TierListCategory.Underrated;

                await _tierLists.SaveEntry(mix, new SongTierListEntry("Popularity", chart.Id, category, place),
                    ct);
            }
        }
    }

    private async Task PopulateOfficialScoresTierList(MixEnum mix,
        List<(Chart Chart, string Username, PhoenixScore Score)> entries, CancellationToken ct)
    {
        var chartLevelGroups = entries.GroupBy(e => (e.Chart.Type, e.Chart.Level))
            .ToDictionary(g => g.Key,
                g => (IDictionary<string, IDictionary<Guid, PhoenixScore>>)g.GroupBy(e => e.Username)
                    .ToDictionary(gu => gu.Key,
                        gu => (IDictionary<Guid, PhoenixScore>)gu.ToDictionary(u => u.Chart.Id, u => u.Score)));

        foreach (var group in chartLevelGroups)
        {
            var tierListEntries =
                TierListProcessor.ProcessIntoTierList(group.Value, group.Key.Level, "Official Scores");
            foreach (var entry in tierListEntries) await _tierLists.SaveEntry(mix, entry, ct);
        }
    }

    private static double StdDev(IEnumerable<int> values, bool asSample)
    {
        var list = values.ToArray();
        double mean = list.Sum() / (double)list.Length;
        var sumOfSquares = list.Select(value => (value - mean) * (value - mean)).Sum();
        return Math.Sqrt(sumOfSquares / (asSample ? list.Length - 1 : list.Length));
    }

    /// <summary>
    ///     Run-scoped player-id cache: each username hits EnsurePlayers once, plus once more
    ///     if a later board finally supplies the avatar a rating board couldn't.
    /// </summary>
    private sealed class SweepPlayerCache
    {
        private readonly Dictionary<string, (int Id, bool HasAvatar)> _players =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly IOfficialSnapshotRepository _snapshots;
        private readonly MixEnum _mix;
        private readonly DateTimeOffset _seenAt;

        public SweepPlayerCache(IOfficialSnapshotRepository snapshots, MixEnum mix, DateTimeOffset seenAt)
        {
            _snapshots = snapshots;
            _mix = mix;
            _seenAt = seenAt;
        }

        public async Task<IReadOnlyDictionary<string, int>> Resolve(
            IReadOnlyCollection<(string Username, Uri? Avatar)> players, CancellationToken ct)
        {
            var toEnsure = players
                .Where(p => !_players.TryGetValue(p.Username, out var known) ||
                            (!known.HasAvatar && p.Avatar != null))
                .GroupBy(p => p.Username, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(p => p.Avatar != null).First())
                .ToArray();
            if (toEnsure.Length > 0)
            {
                var ensured = await _snapshots.EnsurePlayers(_mix, toEnsure, _seenAt, ct);
                foreach (var player in ensured)
                    _players[player.Username] = (player.Id, player.Avatar != null);
            }

            return players.Select(p => p.Username).Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(u => u, u => _players[u].Id, StringComparer.OrdinalIgnoreCase);
        }
    }
}
