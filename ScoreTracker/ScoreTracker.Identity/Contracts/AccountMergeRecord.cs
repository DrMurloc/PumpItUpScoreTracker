namespace ScoreTracker.Identity.Contracts;

[ExcludeFromCodeCoverage]
public sealed record AccountMergeRecord(
    Guid Id,
    Guid SurvivorUserId,
    Guid RetiredUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset PurgeAfter)
{
}
