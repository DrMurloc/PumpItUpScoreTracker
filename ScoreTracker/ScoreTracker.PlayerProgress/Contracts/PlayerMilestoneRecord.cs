namespace ScoreTracker.PlayerProgress.Contracts;

[ExcludeFromCodeCoverage]
public sealed record PlayerMilestoneRecord(
    MilestoneKind Kind,
    Guid? SessionId,
    DateTimeOffset OccurredAt,
    double? OldValue,
    double? NewValue,
    string? Title,
    string? Detail);
