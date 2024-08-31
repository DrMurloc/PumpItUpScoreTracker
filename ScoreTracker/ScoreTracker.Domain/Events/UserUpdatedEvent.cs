namespace ScoreTracker.Domain.Events
{
    public sealed record UserUpdatedEvent(Guid UserId, string? Country, bool IsPublic)
    {
    }
}
