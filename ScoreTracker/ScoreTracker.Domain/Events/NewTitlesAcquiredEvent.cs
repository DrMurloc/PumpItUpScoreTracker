namespace ScoreTracker.Domain.Events;

public sealed record NewTitlesAcquiredEvent(Guid UserId, IEnumerable<string> NewTitles,
    IDictionary<string, string> ParagonUpgrades)
{
}