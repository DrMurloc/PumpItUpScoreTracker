using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records;

public sealed record GameCardRecord(Name GameTag, string Id, bool IsActive)
{
}