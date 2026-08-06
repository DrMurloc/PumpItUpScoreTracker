using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    /// <summary>
    ///     Projects what the player would score on charts they have not played.
    ///     <paramref name="ChartType" /> null projects both and prices against the merged
    ///     top-50; naming a type scopes the whole projection to that pool, which is what the
    ///     Phoenix 2 Singles/Doubles selector needs.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record ProjectPumbilityGainsQuery(Guid UserId, MixEnum Mix = MixEnum.Phoenix,
        ChartType? ChartType = null) : IQuery<PumbilityProjection>;
}
