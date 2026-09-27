using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Events
{
    // The titles piugame showed on an account whose official import changed no score. An import
    // that did change scores carries them on its PlayerScoresUpdatedEvent, where they ride the
    // snapshot card; with no card coming, these get their own announcement. SessionId is the
    // import's session, so a badge minted here belongs to the run that found it.
    [ExcludeFromCodeCoverage]
    public sealed record TitlesDetectedEvent(Guid UserId, IEnumerable<string> TitlesFound, MixEnum Mix,
        Guid? SessionId = null)
    {
    }
}
