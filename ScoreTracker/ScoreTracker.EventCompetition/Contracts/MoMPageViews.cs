using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.EventCompetition.Contracts;

/// <summary>A season as the pages name it; <see cref="IsLive" /> is the clock's answer, not a stored flag.</summary>
[ExcludeFromCodeCoverage]
public sealed record MoMSeasonSummary(Guid Id, string Name, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsLive);

/// <summary>
///     One ranked session on a board (§11.2): the player it belongs to, which of their sessions
///     it is, and the stored cache columns the row prints. Boards rank sessions, so a player
///     may appear several times (D16).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMBoardRow(int Place, Guid SessionId, Guid UserId, User? Player, int SessionNumber,
    int TotalScore, int ChartsPlayed, double AverageBalancedLevel, TimeSpan Downtime, DateTimeOffset PublishedAt,
    Uri? VideoUrl);

/// <summary>The viewer's best session on a board, and how many they have played on it.</summary>
[ExcludeFromCodeCoverage]
public sealed record MoMStanding(int Place, int Of, Guid SessionId, int TotalScore, int ChartsPlayed,
    TimeSpan Downtime, int SessionCount);

[ExcludeFromCodeCoverage]
public sealed record MoMBoardView(Guid BoardId, ChartType ChartType, MixEnum Mix, TimeSpan Window,
    IReadOnlyList<MoMBoardRow> Rows, MoMStanding? Viewer);

/// <summary>
///     The Season page's whole read: the season, the boards the viewer's mix runs (Doubles
///     first), and the neighbouring seasons for the previous / next links that keep the
///     archive crawlable without a seasons route (§11.8).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMSeasonPage(MoMSeasonSummary Season, IReadOnlyList<MoMBoardView> Boards,
    MoMSeasonSummary? Previous, MoMSeasonSummary? Next);

/// <summary>Where each of a session's four numbers places it among the board's sessions (equals share the better place).</summary>
[ExcludeFromCodeCoverage]
public sealed record MoMLeverPlaces(int Charts, int Difficulty, int Grade, int Downtime, int Of);

/// <summary>Another session on the same board, with its four numbers — the marks and the compare picker read these.</summary>
[ExcludeFromCodeCoverage]
public sealed record MoMBoardSessionSummary(Guid SessionId, Guid UserId, User? Player, int Place, int SessionNumber,
    MoMLevers Levers);

/// <summary>The owner's session in another season of the same lineage — a candidate for the season comparison.</summary>
[ExcludeFromCodeCoverage]
public sealed record MoMPastSession(Guid SessionId, MoMSeasonSummary Season, int TotalScore, DateTimeOffset PublishedAt);

[ExcludeFromCodeCoverage]
public sealed record MoMSessionView(
    Guid SessionId,
    MoMSeasonSummary Season,
    Guid BoardId,
    ChartType ChartType,
    MixEnum Mix,
    TimeSpan Window,
    Guid UserId,
    User? Player,
    DateTimeOffset? PublishedAt,
    Uri? VideoUrl,
    int TotalScore,
    int Place,
    int Of,
    MoMLevers Levers,
    MoMLeverPlaces Places,
    IReadOnlyList<MoMTimedChart> Charts,
    IReadOnlyList<MoMBoardSessionSummary> BoardSessions,
    IReadOnlyList<MoMPastSession> OwnersPastSessions)
{
    public bool IsDraft => PublishedAt == null;
}

/// <summary>
///     Two sessions side by side. <see cref="SameBoard" /> is the same-board mode; otherwise the
///     two are seasons of one lineage and <see cref="Repricing" /> re-prices the older under the
///     newer (D20) — <see cref="OlderIsMine" /> says which side that was.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMComparison(
    Guid SessionId,
    Guid OtherSessionId,
    MoMLevers Mine,
    MoMLevers Theirs,
    User? Other,
    MoMSeasonSummary OtherSeason,
    bool SameBoard,
    IReadOnlyList<MoMSharedChart> Shared,
    MoMRepricingSplit? Repricing,
    bool OlderIsMine);

[ExcludeFromCodeCoverage]
public sealed record MoMSeasonBoardListing(Guid BoardId, ChartType ChartType, int SessionCount, User? Winner,
    int? WinningScore, int? ViewerPlace, int? ViewerScore);

[ExcludeFromCodeCoverage]
public sealed record MoMSeasonListing(MoMSeasonSummary Season, IReadOnlyList<MoMSeasonBoardListing> Boards);

[ExcludeFromCodeCoverage]
public sealed record MoMBoardLocator(Guid SeasonId, ChartType ChartType, MixEnum Mix, bool IsLive);
