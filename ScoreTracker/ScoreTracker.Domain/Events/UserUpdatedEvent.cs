namespace ScoreTracker.Domain.Events
{
    [ExcludeFromCodeCoverage]
    public sealed record UserUpdatedEvent(Guid UserId, string? Country, bool IsPublic)
    {
    }
}
