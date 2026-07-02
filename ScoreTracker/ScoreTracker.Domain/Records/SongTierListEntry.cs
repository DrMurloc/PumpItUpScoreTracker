
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records;

[ExcludeFromCodeCoverage]
public sealed record SongTierListEntry(Name TierListName, Guid ChartId, TierListCategory Category, int Order)
{
}
