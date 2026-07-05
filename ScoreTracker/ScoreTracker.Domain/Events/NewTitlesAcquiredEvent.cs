using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Events;

[ExcludeFromCodeCoverage]
public sealed record NewTitlesAcquiredEvent(Guid UserId, IEnumerable<string> NewTitles,
    IDictionary<string, string> ParagonUpgrades, MixEnum Mix)
{
}
