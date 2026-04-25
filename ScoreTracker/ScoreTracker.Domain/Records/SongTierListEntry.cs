
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records;

[ExcludeFromCodeCoverage]
public sealed record SongTierListEntry(Name TierListName, Guid ChartId, TierListCategory Category, int Order)
{
}
