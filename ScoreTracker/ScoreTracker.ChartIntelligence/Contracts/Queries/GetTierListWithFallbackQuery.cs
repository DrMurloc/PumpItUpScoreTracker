using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries
{
    /// <summary>
    ///     Same entries as <see cref="GetTierListQuery" />, plus whether the result is the
    ///     Phoenix list standing in for an empty Phoenix2 one — the UI renders that as a
    ///     "provisional" badge (locked decision, plan doc).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetTierListWithFallbackQuery(Name TierListName, MixEnum Mix = MixEnum.Phoenix)
        : IQuery<TierListResult>
    {
    }
}
