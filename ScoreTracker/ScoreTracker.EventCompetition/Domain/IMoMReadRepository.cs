using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Domain;

/// <summary>A board with its frozen configuration — snapshot, window and repeat rule included.</summary>
internal sealed record MoMBoardInfo(Guid Id, Guid SeasonId, MixEnum Mix, ChartType ChartType,
    TournamentConfiguration Configuration);

/// <summary>
///     A session row as stored: the derived cache columns beside the chart rows (§6). A null
///     <see cref="PublishedAt" /> is a draft, which never reaches a board.
/// </summary>
internal sealed record MoMStoredSession(Guid Id, Guid BoardId, Guid UserId, DateTimeOffset? PublishedAt,
    int TotalScore, int ChartsPlayed, TimeSpan Downtime, double AverageDifficulty, double AverageGrade,
    int LowestLevel, int HighestLevel, Uri? VideoUrl, DateTimeOffset CreatedAt);

internal sealed record MoMStoredSessionChart(Guid SessionId, int Ordinal, Guid ChartId, PhoenixScore Score,
    PhoenixPlate Plate, bool IsBroken, int SessionScore, int BonusPoints, DateTimeOffset? PlayedAt);

/// <summary>
///     The read side of the MoM tables for the 4a surfaces (docs/design/march-of-murlocs.md
///     §12.2). Rows come back as stored; ranking, the four numbers and the re-pricing are the
///     Domain's job, so no two readers can disagree about the same session. Seasons come
///     newest first.
/// </summary>
internal interface IMoMReadRepository
{
    Task<IReadOnlyList<MoMSeason>> GetSeasons(CancellationToken cancellationToken);
    Task<IReadOnlyList<MoMBoardInfo>> GetBoards(IEnumerable<Guid> seasonIds, CancellationToken cancellationToken);
    Task<MoMBoardInfo?> GetBoard(Guid boardId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MoMStoredSession>> GetPublishedSessions(IEnumerable<Guid> boardIds,
        CancellationToken cancellationToken);
    Task<MoMStoredSession?> GetSession(Guid sessionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MoMStoredSessionChart>> GetSessionCharts(IEnumerable<Guid> sessionIds,
        CancellationToken cancellationToken);
}
