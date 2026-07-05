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

    /// <summary>Cohort lookup: players whose competitive level is within ±range of the given level.</summary>
    Task<IEnumerable<Guid>> GetPlayersByCompetitiveRange(MixEnum mix, ChartType? chartType, double competitiveLevel,
        double range, CancellationToken cancellationToken);
}
