
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records;

public sealed record SongTierListEntry(Name TierListName, Guid ChartId, TierListCategory Category, int Order)
{
}