using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Domain;

internal interface IScoreHighlightRepository
{
    /// <summary>
    ///     Upserts by (user, mix, session, chart): existing rows OR-in the new flags,
    ///     missing rows are created. Both the capture consumer and the rating saga's
    ///     competitive-improver pass write through this, in either order.
    /// </summary>
    Task UpsertFlags(MixEnum mix, Guid userId, IEnumerable<ScoreHighlightWrite> highlights,
        CancellationToken cancellationToken);

    Task<IEnumerable<ScoreHighlightRecord>> GetHighlights(MixEnum mix, Guid userId, DateTimeOffset since,
        DateTimeOffset until, CancellationToken cancellationToken);
}

internal sealed record ScoreHighlightWrite(
    Guid ChartId,
    Guid? SessionId,
    DateTimeOffset OccurredAt,
    HighlightFlag Flags,
    int Level,
    double? ScoringLevel);
