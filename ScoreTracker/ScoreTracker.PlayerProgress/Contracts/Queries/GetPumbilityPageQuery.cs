using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    /// <summary>
    ///     The PUMBILITY page's single read. <paramref name="Pool" /> null is the merged
    ///     top-50; naming a type scopes total, bar, curve and targets to that pool, which is
    ///     what Phoenix 2's Singles/Doubles selector switches.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetPumbilityPageQuery(Guid UserId, MixEnum Mix = MixEnum.Phoenix,
        ChartType? Pool = null) : IQuery<PumbilityPageRecord>;
}
