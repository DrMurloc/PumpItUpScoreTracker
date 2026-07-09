using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Events
{
    // SessionId is set when the detecting import also saved scores (so the session's snapshot
    // card will carry these completions); null when the run saved no scores, in which case the
    // titles get their own announcement instead.
    [ExcludeFromCodeCoverage]
    public sealed record TitlesDetectedEvent(Guid UserId, IEnumerable<string> TitlesFound, MixEnum Mix,
        Guid? SessionId = null)
    {
    }
}
