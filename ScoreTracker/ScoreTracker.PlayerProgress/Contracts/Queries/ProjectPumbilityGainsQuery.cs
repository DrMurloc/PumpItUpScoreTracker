using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    /// <summary>
    ///     Projects what the player would score on charts they have not played.
    ///     <paramref name="ChartType" /> null projects both and prices against the merged
    ///     top-50; naming a type scopes the whole projection to that pool, which is what the
    ///     Phoenix 2 Singles/Doubles selector needs. <paramref name="Energy" /> is the rung of the
    ///     peers the scores are read at (D51): the PUMBILITY page passes its select, every other
    ///     caller reads Great (D54). The rung is applied when the cached sweep is priced, so the
    ///     three energies share one sweep.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record ProjectPumbilityGainsQuery(Guid UserId, MixEnum Mix = MixEnum.Phoenix,
        ChartType? ChartType = null, Energy Energy = Energy.Great) : IQuery<PumbilityProjection>;
}
