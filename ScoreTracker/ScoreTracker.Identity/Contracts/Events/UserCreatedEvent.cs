namespace ScoreTracker.Identity.Contracts.Events
{
    [ExcludeFromCodeCoverage]
    public sealed record UserCreatedEvent(Guid UserId)
    {
    }
}
