using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Events
{
    [ExcludeFromCodeCoverage]
    public sealed record TitlesDetectedEvent(Guid UserId, IEnumerable<string> TitlesFound, MixEnum Mix)
    {
    }
}
