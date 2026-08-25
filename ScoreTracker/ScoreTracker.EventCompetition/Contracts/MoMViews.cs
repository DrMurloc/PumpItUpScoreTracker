using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.EventCompetition.Contracts;

/// <summary>
///     Enough of a season's identity to name it and build its dated URL:
///     quarterly seasons route by (Year, Quarter), the off-grid legacy seasons by Name.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMSeasonRef(Guid Id, string Name, int? Year, int? Quarter);

[ExcludeFromCodeCoverage]
public sealed record MoMBoardSummary(Guid BoardId, MixEnum Mix, ChartType ChartType, int SessionCount);

/// <summary>
///     One season with its boards and neighbours. Previous/Next walk StartsAt order across
///     the whole archive (legacy seasons included) — they are what keeps every season
///     reachable by link without a directory route (§11.8).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMSeasonView(Guid Id, string Name, int? Year, int? Quarter,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsLive,
    IReadOnlyList<MoMBoardSummary> Boards, MoMSeasonRef? Previous, MoMSeasonRef? Next)
{
    public MoMSeasonRef Ref => new(Id, Name, Year, Quarter);
}

/// <summary>
///     One ranked row of a board. Boards rank sessions, not players (D16) — the same user
///     may hold several rows — and AverageDifficulty is the season's frozen BALANCED level,
///     not the folder number (§11.6). AverageGrade is the mean PhoenixLetterGrade ordinal.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMBoardRow(int Place, Guid SessionId, Guid UserId, string UserName,
    Uri? ProfileImage, string? Country, int TotalScore, int ChartsPlayed,
    double AverageDifficulty, double AverageGrade, int LowestLevel, int HighestLevel,
    TimeSpan RestTime, DateTimeOffset PublishedAt, Uri? VideoUrl);

[ExcludeFromCodeCoverage]
public sealed record MoMBoardView(Guid BoardId, MoMSeasonRef Season, MixEnum Mix,
    ChartType ChartType, IReadOnlyList<MoMBoardRow> Rows);

/// <summary>
///     One chart of a session. SessionScore is the points the chart paid under the board's
///     frozen configuration (BonusPoints of it from the chart-level snapshot bump);
///     BalancedLevel is what actually priced it (§11.6).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMSessionChartRow(int Ordinal, Guid ChartId, int Score, PhoenixPlate Plate,
    bool IsBroken, int SessionScore, int BonusPoints, DateTimeOffset? PlayedAt, double BalancedLevel);

/// <summary>
///     One session in full. PublishedAt null means draft (D17) — served only to its owner.
///     Place is the session's rank on its board, null for drafts. MaxTime and AllowRepeats
///     come from the board's frozen configuration, so an editor enforces exactly the rules
///     the board was created under.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMSessionView(Guid Id, Guid BoardId, MoMSeasonRef Season, MixEnum Mix,
    ChartType ChartType, Guid UserId, string UserName, DateTimeOffset? PublishedAt,
    int TotalScore, int ChartsPlayed, TimeSpan RestTime, double AverageDifficulty,
    double AverageGrade, int LowestLevel, int HighestLevel, Uri? VideoUrl, int? Place,
    TimeSpan MaxTime, bool AllowRepeats, IReadOnlyList<MoMSessionChartRow> Charts)
{
    public bool IsDraft => PublishedAt == null;
}

/// <summary>
///     One board's line in the Past Seasons dialog: how big it was, who won it, and how the
///     viewer did — YourPlace/YourScore/YourBestSessionId are null for a season the viewer
///     sat out (or an anonymous viewer).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMBoardStanding(Guid BoardId, MixEnum Mix, ChartType ChartType,
    int SessionCount, string? WinnerName, int? WinnerScore, int? YourPlace, int? YourScore,
    Guid? YourBestSessionId);

[ExcludeFromCodeCoverage]
public sealed record MoMSeasonListing(MoMSeasonRef Season, DateTimeOffset StartsAt,
    DateTimeOffset EndsAt, bool IsLive, IReadOnlyList<MoMBoardStanding> Boards);

/// <summary>
///     One entry of a draft as the editor holds it: the raw play, not its points — the
///     save handler prices every entry under the board's frozen configuration. PlayedAt
///     rides along from the journal import and is null for hand-typed entries.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMDraftEntry(Guid ChartId, int Score, PhoenixPlate Plate, bool IsBroken,
    DateTimeOffset? PlayedAt);

/// <summary>
///     The D20 re-rating split: the same session — the same charts, the same scores —
///     re-priced under another board's whole frozen configuration. The two deltas are each
///     isolated against the original (snapshot swapped alone, tables swapped alone); the
///     effects multiply, so they deliberately do not sum to the total. RepricedChartPoints
///     is each chart's points under the full target configuration, which is what a
///     cross-season common-charts comparison prices both sides in.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMSessionReprice(int OriginalTotal, int RepricedTotal,
    int ChartsReratedCount, int ChartReratingDelta, int TableRecutDelta,
    IReadOnlyDictionary<Guid, int> RepricedChartPoints);
