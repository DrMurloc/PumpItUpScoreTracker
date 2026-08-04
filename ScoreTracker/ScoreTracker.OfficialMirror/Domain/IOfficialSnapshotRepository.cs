using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     The snapshot model's storage: run headers, the board/player dimensions, and the
///     per-snapshot placement and popularity facts. Only sealed snapshots are data —
///     every read path resolves through the latest CompletedAt.
/// </summary>
internal interface IOfficialSnapshotRepository
{
    Task<int> CreateRun(MixEnum mix, bool isBaseline, DateTimeOffset startedAt, CancellationToken ct);
    /// <summary>Checkpoints stage/counts and stamps the run's heartbeat.</summary>
    Task UpdateProgress(int snapshotId, string stage, int boardsExpected, int boardsWritten, int boardsSkipped,
        DateTimeOffset at, CancellationToken ct);
    Task MarkFailed(int snapshotId, string error, CancellationToken ct);
    Task Seal(int snapshotId, DateTimeOffset completedAt, CancellationToken ct);

    /// <summary>Deletes unsealed runs older than the cutoff, including their placement/popularity/highlight rows.</summary>
    Task PurgeUnsealed(MixEnum mix, DateTimeOffset olderThan, CancellationToken ct);

    /// <summary>
    ///     True when an unsealed run has heartbeated since the cutoff — the only kind of
    ///     run the overlap guard respects. A run whose process died stops beating and no
    ///     longer counts, however recently it started.
    /// </summary>
    Task<bool> HasLiveRun(MixEnum mix, DateTimeOffset heartbeatCutoff, CancellationToken ct);
    Task<SnapshotRun?> GetLatestSealed(MixEnum mix, CancellationToken ct);

    /// <summary>The sealed snapshot immediately preceding the given one — the diff baseline.</summary>
    Task<SnapshotRun?> GetSealedBefore(MixEnum mix, int snapshotId, CancellationToken ct);

    Task<IReadOnlyList<SnapshotRun>> GetSealedAscending(MixEnum mix, CancellationToken ct);
    Task<IReadOnlyList<SnapshotRun>> GetRecentRuns(MixEnum mix, int count, CancellationToken ct);
    Task<bool> AnySealed(MixEnum mix, CancellationToken ct);

    Task<IReadOnlyList<BoardDimension>> GetBoards(MixEnum mix, CancellationToken ct);
    Task<BoardDimension> EnsureBoard(MixEnum mix, string leaderboardType, string name, Guid? chartId,
        string? chartType, int? level, CancellationToken ct);

    /// <summary>
    ///     Upserts by (mix, username): creates missing players, stamps LastSeenAt, and
    ///     refreshes a changed avatar — a null incoming avatar keeps the stored one.
    /// </summary>
    Task<IReadOnlyList<PlayerDimension>> EnsurePlayers(MixEnum mix,
        IReadOnlyCollection<(string Username, Uri? Avatar)> players, DateTimeOffset seenAt, CancellationToken ct);

    Task<IReadOnlyList<PlayerDimension>> GetPlayers(MixEnum mix, CancellationToken ct);

    Task WritePlacements(int snapshotId, IReadOnlyCollection<PlacementRow> rows, CancellationToken ct);
    Task<IReadOnlyList<PlacementRow>> GetPlacements(int snapshotId, PlacementScope scope, CancellationToken ct);

    /// <summary>
    ///     Clears one snapshot's supplemented rows, leaving every official row alone. The
    ///     roll-up runs this first so a re-press of the admin button replaces its own output
    ///     rather than doubling it.
    /// </summary>
    Task DeleteSupplementedPlacements(int snapshotId, CancellationToken ct);

    /// <summary>
    ///     True once any sealed snapshot in this mix holds supplemented rows. False means the
    ///     next roll-up is the supplemented series' own week one — which is a different
    ///     question from whether the official sweep was a baseline, and the reason the first
    ///     roll-up emits no highlights instead of several hundred simultaneous debuts.
    /// </summary>
    Task<bool> AnySupplemented(MixEnum mix, CancellationToken ct);

    /// <summary>How many players and rows one snapshot's supplemented reading added.</summary>
    Task<(int Players, int Rows)> CountSupplemented(int snapshotId, CancellationToken ct);
    Task WritePopularity(int snapshotId, IReadOnlyCollection<(Guid ChartId, int Place)> rows, CancellationToken ct);
    Task DeletePopularity(int snapshotId, CancellationToken ct);

    Task<IReadOnlyList<(Guid ChartId, int Place)>> GetPopularity(int snapshotId, CancellationToken ct);

    Task<IReadOnlyList<PlayerDimension>> GetPlayersByIds(IReadOnlyCollection<int> playerIds, CancellationToken ct);
    Task<PlayerDimension?> GetPlayerByUsername(MixEnum mix, string username, CancellationToken ct);

    /// <summary>The import-linked mirror player for a site account, if the link exists.</summary>
    Task<PlayerDimension?> GetPlayerByUserId(MixEnum mix, Guid userId, CancellationToken ct);

    /// <summary>
    ///     Tags the player search offers: those with a placement in the latest sealed snapshot
    ///     at this reading. Every dim row would include tags an import created but no crawl has
    ///     ever seen, and picking one of those renders a confident profile full of blanks.
    /// </summary>
    Task<IReadOnlyList<string>> GetPlayerNames(MixEnum mix, PlacementScope scope, CancellationToken ct);

    /// <summary>
    ///     Every player id with a placement in any of this mix's snapshots before the given
    ///     one — the all-history "seen" set that makes a debut a debut.
    /// </summary>
    Task<IReadOnlySet<int>> GetSeenPlayerIds(MixEnum mix, int beforeSnapshotId, PlacementScope scope,
        CancellationToken ct);

    /// <summary>One board's rows within one snapshot, in display order.</summary>
    Task<IReadOnlyList<PlacementRow>> GetBoardPlacements(int snapshotId, int leaderboardId, PlacementScope scope,
        CancellationToken ct);

    /// <summary>
    ///     Several boards' rows within one snapshot, in display order. The batch exists because
    ///     a score session touches dozens of charts and the per-board overload would issue a
    ///     query each; every row carries its LeaderboardId, so the caller regroups.
    /// </summary>
    Task<IReadOnlyList<PlacementRow>> GetBoardPlacements(int snapshotId, IReadOnlyCollection<int> leaderboardIds,
        PlacementScope scope, CancellationToken ct);

    /// <summary>Every placement in a snapshot joined with its board dimension.</summary>
    Task<IReadOnlyList<PlacementDetail>> GetPlacementDetails(int snapshotId, PlacementScope scope,
        CancellationToken ct);

    /// <summary>A player's rows across every sealed snapshot, oldest first.</summary>
    Task<IReadOnlyList<PlayerTimelineRow>> GetPlayerTimeline(int playerId, PlacementScope scope,
        CancellationToken ct);

    /// <summary>Popularity rows for the newest sealed snapshots, newest snapshot first.</summary>
    Task<IReadOnlyList<(int SnapshotId, Guid ChartId, int Place)>> GetPopularityHistory(MixEnum mix,
        int snapshots, CancellationToken ct);

    /// <summary>
    ///     A rating board's floor across every sealed snapshot, oldest first: the minimum
    ///     score on the board and how many rows it held (a full board's floor is its
    ///     entry bar).
    /// </summary>
    Task<IReadOnlyList<(int SnapshotId, DateTimeOffset CompletedAt, decimal MinScore, int Count)>>
        GetBoardFloorHistory(MixEnum mix, string boardName, PlacementScope scope, CancellationToken ct);

    /// <summary>One row per distinct unmapped chart; re-sightings refresh LastIdentified.</summary>
    Task UpsertMissingCharts(MixEnum mix, IReadOnlyCollection<MissingChartSighting> sightings,
        DateTimeOffset seenAt, CancellationToken ct);

    Task<IReadOnlyList<MissingChartRow>> GetMissingCharts(MixEnum mix, CancellationToken ct);
    Task DeleteMissingChart(int id, CancellationToken ct);
}
