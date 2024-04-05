namespace ScoreTracker.Domain.Events
{
    public sealed record TitlesDetectedEvent(Guid UserId, IEnumerable<string> TitlesFound)
    {
    }
}
