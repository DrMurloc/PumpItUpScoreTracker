using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     One sweep run: run state while executing, a snapshot once CompletedAt seals it.
/// </summary>
internal sealed record SnapshotRun(int Id, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt,
    bool IsBaseline, string Stage, int BoardsExpected, int BoardsWritten, int BoardsSkipped, string? Error);

internal sealed record BoardDimension(int Id, string LeaderboardType, string Name,
    Guid? ChartId, string? ChartType, int? Level);

/// <param name="LastSeenAt">
///     Refreshed every sweep for a tag that appears on a board, so it is the mirror's own
///     evidence of which of an account's tags is still in use — see the one-user-many-tags
///     note on SupplementRollupSaga.Cohort.
/// </param>
internal sealed record PlayerDimension(int Id, string Username, Uri? Avatar, Guid? UserId,
    DateTimeOffset LastSeenAt = default);

internal sealed record PlacementRow(int LeaderboardId, int PlayerId, int Place, decimal Score,
    bool IsSupplemented = false);

/// <summary>
///     Which reading of a snapshot a placement query wants. There is no default: a snapshot
///     holds two readings and every caller has to say which one it means, because the wrong
///     answer is silent. Official rows feed the record books, the tier-list feed, the
///     cutlines and the Discord digest, and none of those may ever see a supplemented row
///     (supplemented-leaderboards.md §7).
/// </summary>
internal enum PlacementScope
{
    /// <summary>Only what piugame published.</summary>
    OfficialOnly,

    /// <summary>Both readings — the supplemented view, and only the supplemented view.</summary>
    IncludingSupplemented
}

internal sealed record BoardRecordRow(int LeaderboardId, int HighScore, int AchievedSnapshotId);

/// <summary>
///     Best band RANK ever claimed on every OTHER mix's record books — charts keyed by the
///     mix-agnostic ChartId, folders by their nominal (type, level), each source score
///     banded in its own mix's grade table (<see cref="GradeBandLadder" />). The cross-mix
///     reference that keeps a re-clear of a Phoenix-era achievement from reading as a
///     Phoenix 2 world first.
/// </summary>
internal sealed record CrossMixRecordHighs(
    IReadOnlyDictionary<Guid, int> ChartBandRanks,
    IReadOnlyDictionary<(string ChartType, int Level), int> FolderBandRanks)
{
    public static readonly CrossMixRecordHighs Empty = new(
        new Dictionary<Guid, int>(), new Dictionary<(string, int), int>());
}

internal sealed record FolderRecordRow(string ChartType, int Level, int HighScore, int AchievedSnapshotId);

internal sealed record HighlightRow(string Kind, int SortOrder, int? PlayerId, int? DethronedPlayerId,
    int? LeaderboardId, Guid? ChartId, string? ChartType, int? Level, string? GradeBand,
    decimal? Score, decimal? PrevValue, decimal? NewValue);

/// <summary>
///     What a merge attempt did. A finding can outlive the world it was written against —
///     another merge can delete its candidate, or an import can claim the tag for a different
///     account — so refusing is a normal outcome, not an exception.
/// </summary>
internal enum MergeOutcome
{
    Merged,

    /// <summary>One of the two rows no longer exists. Re-pointing would orphan history.</summary>
    PlayerGone,

    /// <summary>Both tags are linked, to different site accounts. They are not one person.</summary>
    DifferentAccounts
}

/// <summary>What the analyzer concluded about a tag that left the boards.</summary>
internal static class VanishVerdicts
{
    /// <summary>Conclusive. Merged unattended, no admin in the loop.</summary>
    public const string Merge = "Merge";

    /// <summary>Two candidates explain the tag comparably well; guessing between them is not on.</summary>
    public const string Ambiguous = "Ambiguous";

    /// <summary>
    ///     The tag left scores behind that should still be ranking. Whatever happened is not
    ///     an ordinary rename — a ban is the usual answer — so it never merges itself.
    /// </summary>
    public const string Suspicious = "Suspicious";

    /// <summary>A candidate fits and nothing contradicts it, but the evidence is thin.</summary>
    public const string Propose = "Propose";

    /// <summary>
    ///     Nothing fits, and every score the tag held has fallen below its board's cut. The
    ///     ordinary way a player leaves the boards: they were passed. Recorded, not actioned.
    /// </summary>
    public const string DroppedOff = "DroppedOff";
}

/// <summary>
///     Why the analyzer reached its verdict, kept whole so the admin desk argues the case
///     rather than asserting it. <paramref name="ExactNonPgMatches" /> is the one that
///     identifies a person: two players sharing a perfect 1,000,000 is a weekly occurrence,
///     two players sharing five identical imperfect scores is not.
/// </summary>
internal sealed record RenameEvidence(int OldPlacements, int BoardsPresent, int ExactNonPgMatches,
    int ExactPerfectGames, int RunnerUpExactMatches, int SuspiciousAbsences, bool AvatarMatched)
{
    public static readonly RenameEvidence None = new(0, 0, 0, 0, 0, 0, false);
}

/// <summary>
///     A tag that was on the chart boards last snapshot and is on nothing at all now, with
///     the analyzer's verdict and the candidate it points at (none, for a tag that simply
///     dropped off).
///     <para>
///         <paramref name="Mix" /> is null on a fresh finding and set once it has been read back
///         from storage — the same lifecycle <paramref name="Id" /> already expresses by being 0
///         until written. The analyzer is mix-agnostic (it compares two snapshots that are a
///         mix's by construction) and the sweep supplies the mix on write; a reader accepting a
///         finding by id needs it back, to say which boards the rename happened on.
///     </para>
/// </summary>
internal sealed record RenameProposal(int Id, int OldPlayerId, int? NewPlayerId, string OldUsername,
    string? NewUsername, string Verdict, RenameEvidence Evidence, string Status, int CreatedSnapshotId,
    MixEnum? Mix = null);

/// <summary>One player's place and score on one chart's board.</summary>
internal sealed record PlayerChartPlacement(int PlayerId, Guid ChartId, int Place, decimal Score);

/// <summary>
///     One player's best published score on one chart, across every snapshot that ever carried it.
///     The level rides along because the caller prices the chart and would otherwise read the
///     catalog for something the board dimension already knows.
/// </summary>
internal sealed record PlayerChartHistoryRow(int PlayerId, Guid ChartId, int Level, int Score);

/// <summary>A placement joined with its board's dimension — the hub read shape.</summary>
internal sealed record PlacementDetail(int PlayerId, int LeaderboardId, string LeaderboardType, string BoardName,
    Guid? ChartId, string? ChartType, int? Level, int Place, decimal Score, bool IsSupplemented = false);

/// <summary>One row of a player's life across snapshots, sealed runs only.</summary>
internal sealed record PlayerTimelineRow(int SnapshotId, DateTimeOffset CompletedAt, string LeaderboardType,
    string BoardName, Guid? ChartId, int Place, decimal Score, bool IsSupplemented = false);

internal static class LeaderboardTypes
{
    public const string Rating = "Rating";
    public const string Chart = "Chart";
}

internal static class HighlightKinds
{
    public const string PumbilityMover = "PumbilityMover";
    public const string BoardsClimbed = "BoardsClimbed";
    public const string NewNumberOne = "NewNumberOne";
    public const string ChartGradeFirst = "ChartGradeFirst";
    public const string FolderGradeFirst = "FolderGradeFirst";

    /// <summary>
    ///     One playerless row per snapshot: PrevValue = chart-board entries that are new,
    ///     NewValue = entries that upscored, Score = distinct players behind them,
    ///     Level = total debut count (every debut also gets its own stored row).
    /// </summary>
    public const string WeeklyPulse = "WeeklyPulse";

    /// <summary>
    ///     Top PUMBILITY value gainers: Score = new pumbility, PrevValue = previous
    ///     pumbility, NewValue = new rank, Level = previous rank.
    /// </summary>
    public const string PumbilityGainer = "PumbilityGainer";

    /// <summary>
    ///     A player's first-ever appearance on a chart board (never seen on any board in
    ///     any earlier snapshot). Score = their best chart-board place this week; ordered
    ///     by it, one row per debut — the pulse row carries the matching total. (Weeks
    ///     materialized before the cap was lifted may hold fewer rows than the total.)
    /// </summary>
    public const string Debut = "Debut";

    /// <summary>
    ///     A landmark PUMBILITY floor, playerless: SortOrder = the rank (100/1000),
    ///     Score = the floor value, PrevValue = last week's, Level = the uniform level
    ///     where 50× SS clears it (SG plates assumed), NewValue = last week's level.
    /// </summary>
    public const string FloorMark = "FloorMark";
}

internal static class ProposalStatuses
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Dismissed = "Dismissed";

    /// <summary>
    ///     Merged by the sweep with nobody watching. Kept distinct from <see cref="Accepted" />
    ///     so the desk can always answer "did a human decide this?" — the merge is a one-way
    ///     door, and which ones the machine walked through on its own is the first thing worth
    ///     knowing when one turns out to be wrong.
    /// </summary>
    public const string AutoAccepted = "AutoAccepted";
}
