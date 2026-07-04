using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries
{
    /// <summary>
    ///     Entries for the requested mix. When the requested mix is Phoenix2 and no entries exist
    ///     yet, the handler silently falls back to the Phoenix list (locked decision: provisional
    ///     P1 data until P2 data accumulates). Callers that need to know a fallback happened use
    ///     <see cref="GetTierListWithFallbackQuery" /> instead.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetTierListQuery(Name TierListName, MixEnum Mix = MixEnum.Phoenix)
        : IQuery<IEnumerable<SongTierListEntry>>
    {
    }
}
