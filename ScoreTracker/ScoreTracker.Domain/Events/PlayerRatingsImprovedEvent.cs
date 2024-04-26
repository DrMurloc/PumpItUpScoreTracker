namespace ScoreTracker.Domain.Events
{
    public sealed record PlayerRatingsImprovedEvent(Guid UserId, int OldTop50, int OldSinglesTop50,
        int OldDoublesTop50, int NewTop50, int NewSinglesTop50, int NewDoublesTop50, double OldCompetitive,
        double NewCompetitive, double OldSinglesCompetitive, double NewSinglesCompetitive, double OldDoublesCompetitive,
        double NewDoublesCompetitive)
    {
    }
}
