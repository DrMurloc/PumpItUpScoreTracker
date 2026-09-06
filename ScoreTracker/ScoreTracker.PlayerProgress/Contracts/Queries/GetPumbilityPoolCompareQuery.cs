using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    /// <summary>
    ///     The Breakdown page's read of the viewer against their peers
    ///     (docs/design/pumbility-overhaul.md D58): where their fifty of each lit type sits against
    ///     the peers' by level, and — for the merged scope only, <paramref name="Pool" /> null,
    ///     since a singles or doubles pool is one type by definition — the peers' average merged
    ///     fifty split by type. Off the same cached sweep the projection uses; the split's own read
    ///     is cached beside it for the sweep's day.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetPumbilityPoolCompareQuery(Guid UserId, MixEnum Mix = MixEnum.Phoenix,
        ChartType? Pool = null) : IQuery<PumbilityPoolCompareRecord>;
}
