namespace ScoreTracker.Domain.Events
{
    [ExcludeFromCodeCoverage]
    public sealed record UserCreatedEvent(Guid UserId)
    {
    }
}
