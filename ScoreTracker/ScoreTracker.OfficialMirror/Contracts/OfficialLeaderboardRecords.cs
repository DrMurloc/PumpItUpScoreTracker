using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>A board-visible player: mirrored tag + avatar, plus the site account when import-linked.</summary>
[ExcludeFromCodeCoverage]
public sealed record OfficialPlayerRecord(int PlayerId, string Username, Uri? AvatarUrl, Guid? UserId);

[ExcludeFromCodeCoverage]
public sealed record WeeklyHighlightsRecord(
    DateTimeOffset SnapshotAt,
    DateTimeOffset? PreviousSnapshotAt,
    IReadOnlyList<OfficialMoverRecord> Movers,
    IReadOnlyList<OfficialBoardsClimbedRecord> BoardsClimbed,
    IReadOnlyList<OfficialGradeFirstRecord> WorldFirsts,
    IReadOnlyList<OfficialNewNumberOneRecord> NewNumberOnes,
    WeeklyPulseRecord? Pulse = null,
    IReadOnlyList<OfficialGainerRecord>? Gainers = null,
    IReadOnlyList<OfficialDebutRecord>? Debuts = null,
    IReadOnlyList<OfficialFloorMarkRecord>? Floors = null);

/// <summary>The week's activity roll-up: new/upscored chart-board entries, the distinct
/// players behind them, and how many players debuted (the Debuts list is a sample).</summary>
[ExcludeFromCodeCoverage]
public sealed record WeeklyPulseRecord(int NewEntries, int UpscoredEntries, int PlayersActive, int DebutCount);

[ExcludeFromCodeCoverage]
public sealed record OfficialGainerRecord(OfficialPlayerRecord Player, decimal PreviousPumbility,
    decimal NewPumbility, int PreviousRank, int NewRank);

[ExcludeFromCodeCoverage]
public sealed record OfficialDebutRecord(OfficialPlayerRecord Player, int BestPlace);

/// <summary>A landmark PUMBILITY floor: the value at the rank and the uniform level where
/// 50× SS clears it (SG plates assumed), with last week's pair for the delta.</summary>
[ExcludeFromCodeCoverage]
public sealed record OfficialFloorMarkRecord(int Rank, decimal Value, decimal? PreviousValue,
    int? LevelForSS, int? PreviousLevelForSS);

[ExcludeFromCodeCoverage]
public sealed record OfficialMoverRecord(OfficialPlayerRecord Player, int PreviousRank, int NewRank,
    decimal Pumbility);

/// <summary>NewBoards splits the tally: how many of the boards were first-time entries
/// (null on rows stored before the split existed).</summary>
[ExcludeFromCodeCoverage]
public sealed record OfficialBoardsClimbedRecord(OfficialPlayerRecord Player, int BoardsClimbed,
    int NetPlacesGained, int? NewBoards = null);

/// <summary>A first-ever grade band on a board; folder firsts carry the folder identity too.</summary>
[ExcludeFromCodeCoverage]
public sealed record OfficialGradeFirstRecord(OfficialPlayerRecord Player, Guid? ChartId, string? ChartType,
    int? Level, string GradeBand, int Score, bool IsFolderFirst);

[ExcludeFromCodeCoverage]
public sealed record OfficialNewNumberOneRecord(OfficialPlayerRecord Player, Guid ChartId, int Score,
    OfficialPlayerRecord? Dethroned);

[ExcludeFromCodeCoverage]
public sealed record OfficialRankingsRecord(DateTimeOffset? SnapshotAt, bool RatingIsOfficial,
    IReadOnlyList<OfficialRankingRecord> Rankings);

[ExcludeFromCodeCoverage]
public sealed record OfficialRankingRecord(int Rank, int? PreviousRank, OfficialPlayerRecord Player,
    decimal Rating, int BoardsInTop, RecapPlayerType? PlayerType);

[ExcludeFromCodeCoverage]
public sealed record OfficialPlayerProfileRecord(
    OfficialPlayerRecord Player,
    RecapPlayerType? PlayerType,
    decimal? Pumbility,
    int? PumbilityRank,
    int? PumbilityRankDelta,
    int BoardsInTop,
    int NumberOnes,
    int BestPlace,
    int TopTens,
    IReadOnlyList<OfficialPlayerHistoryPoint> History,
    IReadOnlyList<OfficialPlayerChartRecord> Placements);

[ExcludeFromCodeCoverage]
public sealed record OfficialPlayerHistoryPoint(DateTimeOffset At, decimal? Pumbility, int? PumbilityRank,
    int BoardsInTop);

/// <summary>One chart on a player's list — mirrored board rows only, every one with a Place.</summary>
[ExcludeFromCodeCoverage]
public sealed record OfficialPlayerChartRecord(Guid ChartId, int? Place, int? PlaceDelta, int Score,
    int ComputedRating);

[ExcludeFromCodeCoverage]
public sealed record OfficialPopularityRecord(Guid ChartId, int Place, int? PreviousPlace,
    IReadOnlyList<int> RecentPlaces);

[ExcludeFromCodeCoverage]
public sealed record ImportRunRecord(int SnapshotId, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt,
    bool IsBaseline, string Stage, int BoardsExpected, int BoardsWritten, int BoardsSkipped, string? Error);
