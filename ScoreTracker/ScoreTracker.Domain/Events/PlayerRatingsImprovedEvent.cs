using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Events
{
    /// <summary>
    ///     The top-50 pools and the co-op rating ride as doubles: they are PUMBILITY, and nothing
    ///     below the presentation layer rounds it.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record PlayerRatingsImprovedEvent(Guid UserId, double OldTop50, double OldSinglesTop50,
        double OldDoublesTop50, double NewTop50, double NewSinglesTop50, double NewDoublesTop50, double OldCompetitive,
        double NewCompetitive, double OldSinglesCompetitive, double NewSinglesCompetitive, double OldDoublesCompetitive,
        double NewDoublesCompetitive, double CoOpRating, int PassCount, MixEnum Mix, Guid? SessionId = null)
    {
    }
}
