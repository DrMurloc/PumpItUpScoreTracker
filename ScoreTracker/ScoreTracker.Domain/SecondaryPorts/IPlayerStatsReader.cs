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
    ///     Peer lookup on the PUMBILITY ladder: players whose total PUMBILITY pool sits at or above
    ///     <paramref name="minimumTotal" /> and below <paramref name="maximumTotalExclusive" /> — a
    ///     rung band expressed as the pool values its rungs start at (docs/design/pumbility-overhaul.md
    ///     §4.8). Half-open on purpose: a rung's <c>NextThreshold</c> is where the next rung starts,
    ///     and a pool exactly on it belongs above.
    /// </summary>
    Task<IEnumerable<Guid>> GetPlayersByPumbilityRange(MixEnum mix, double minimumTotal, double maximumTotalExclusive,
        CancellationToken cancellationToken);
}
