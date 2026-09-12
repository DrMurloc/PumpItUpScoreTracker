using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     The mirror's own view of board players' priced charts — every chart it can see for a
///     player, with what that chart is worth to them.
///     <para>
///         Vertical-internal, and deliberately richer than the Domain port beside it. The census
///         needs only slot order, so <c>IOfficialPoolReader</c> hands it chart ids and nothing
///         else; the Hardmode board needs the values, and a board total built from ids alone
///         would be the same number for every player. Both come off one read — the pricing lives
///         here because a board row carries no plate and inferring one is this vertical's
///         business.
///     </para>
/// </summary>
internal interface IOfficialPoolSource
{
    /// <summary>
    ///     Every board player the mirror can price, with their charts in descending value order.
    ///     Players already linked to a site account are excluded: they count once, through their
    ///     own records, which carry real plates.
    /// </summary>
    Task<IReadOnlyList<OfficialPricedPool>> GetPricedPools(MixEnum mix, CancellationToken cancellationToken);
}

/// <summary>One board player's priced charts, best first.</summary>
internal sealed record OfficialPricedPool(int OfficialPlayerId, IReadOnlyList<OfficialPricedChart> Charts);

internal sealed record OfficialPricedChart(Guid ChartId, ChartType ChartType, double Value);
