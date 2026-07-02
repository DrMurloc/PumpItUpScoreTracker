using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     Chart Intelligence's scoring-level projection (rearch F4): the community-derived
///     "effective" difficulty of a chart for scoring purposes. Written by the scoring
///     difficulty recompute; read by anything that wants effective rather than official
///     levels (randomizer draws, M.o.M. snapshots, tier-list displays).
/// </summary>
public interface IChartScoringLevelRepository
{
    /// <summary>Null clears the level (insufficient data).</summary>
    Task SaveScoringLevel(MixEnum mix, Guid chartId, double? scoringLevel, CancellationToken cancellationToken);

    Task<IDictionary<Guid, double>> GetScoringLevels(MixEnum mix, CancellationToken cancellationToken);
}
