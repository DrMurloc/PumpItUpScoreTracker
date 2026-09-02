using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     Player Progression's published read contract (ADR-001 D3 "pull"): consumers outside
///     Progression read player stats through this, never through
///     <see cref="IPlayerStatsRepository" /> (which becomes Progression-internal at P5).
/// </summary>
public interface IPlayerStatsReader
{
    Task<PlayerStatsRecord> GetStats(MixEnum mix, Guid userId, CancellationToken cancellationToken);

    Task<IEnumerable<PlayerStatsRecord>> GetStats(MixEnum mix, IEnumerable<Guid> userIds,
        CancellationToken cancellationToken);

    /// <summary>Peer lookup: players whose competitive level is within ±range of the given level.</summary>
    Task<IEnumerable<Guid>> GetPlayersByCompetitiveRange(MixEnum mix, ChartType? chartType, double competitiveLevel,
        double range, CancellationToken cancellationToken);

    /// <summary>
    ///     Peer lookup on the pool of one chart type (docs/design/pumbility-overhaul.md D53): players
    ///     whose PUMBILITY pool of <paramref name="chartType" /> — the stats row's per-type top-fifty
    ///     sum, stored unrounded — sits at or above <paramref name="minimumPool" /> and at or below
    ///     <paramref name="maximumPool" />. Inclusive both ends: the window is a distance from the
    ///     viewer's own pool, not a ladder of rungs with a next start. Singles reads the singles
    ///     pool, doubles the doubles pool; any other type reads the merged total.
    /// </summary>
    Task<IEnumerable<Guid>> GetPlayersByPoolOfType(MixEnum mix, ChartType chartType, double minimumPool,
        double maximumPool, CancellationToken cancellationToken);
}
