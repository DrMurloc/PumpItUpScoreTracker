namespace ScoreTracker.Identity.Infrastructure.Entities;

internal sealed class MergeRequestEntity
{
    public Guid Id { get; set; }
    public Guid SurvivorUserId { get; set; }
    public Guid RetiredUserId { get; set; }

    public string MovedLogins { get; set; } = string.Empty;

    public string RetiredUserSnapshot { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset PurgeAfter { get; set; }
    public DateTimeOffset? PurgedAt { get; set; }
}
