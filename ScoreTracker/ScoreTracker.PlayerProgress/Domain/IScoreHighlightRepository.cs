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

    /// <summary>
    ///     Reads highlights for specific sessions (the Sessions page). FK by SessionId, not a
    ///     date window — highlight OccurredAt is the batch-drain time, minutes past the
    ///     journal rows, so a row-time window drops the freshest session's flags.
    /// </summary>
    Task<IEnumerable<ScoreHighlightRecord>> GetHighlightsBySessions(Guid userId, IEnumerable<Guid> sessionIds,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Every flagged highlight across all users on or after the cutoff — the community-feed
    ///     backfill source (session-keyed rows only). The tuple carries the owning user + mix.
    /// </summary>
    Task<IEnumerable<(Guid UserId, MixEnum Mix, ScoreHighlightRecord Record)>> GetHighlightsSince(
        DateTimeOffset since, CancellationToken cancellationToken);
}

internal sealed record ScoreHighlightWrite(
    Guid ChartId,
    Guid? SessionId,
    DateTimeOffset OccurredAt,
    HighlightFlags Flags,
    int Level,
    double? ScoringLevel,
    HighlightDetail? Detail = null);
