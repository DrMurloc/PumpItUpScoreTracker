using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records;

[ExcludeFromCodeCoverage]
public sealed record GameCardRecord(Name GameTag, string Id, bool IsActive)
{
}
