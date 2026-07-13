using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    /// <summary>
    ///     The players nearest you on the ladder: within ±<paramref name="Range" /> of
    ///     <paramref name="MyLevel" /> on the chosen competitive dimension (null = combined),
    ///     nearest first, capped at <paramref name="Count" />. No eligibility filter — the
    ///     caller layers privacy/community rules and its own final cut on top.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetCompetitiveNeighborsQuery(
        MixEnum Mix, ChartType? Dimension, double MyLevel, double Range, int Count)
        : IQuery<IReadOnlyList<CompetitiveNeighborRecord>>;
}
