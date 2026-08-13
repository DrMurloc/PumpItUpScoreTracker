namespace ScoreTracker.Domain.Records;

/// <summary>
///     What a player asked to remove from the things they have contributed to other people's
///     views of the game — votes, ratings, challenge entries, tournament results, memberships.
/// </summary>
[Flags]
public enum ContributionDeletionItems
{
    None = 0,
    ChartDifficultyRatings = 2,
    ChartPreferenceRatings = 4,
    CoOpRatings = 8,
    WeeklyAndDailyStep = 16,
    TournamentResults = 32,
    CommunityMemberships = 64,

    Everything = ChartDifficultyRatings | ChartPreferenceRatings | CoOpRatings |
                 WeeklyAndDailyStep | TournamentResults | CommunityMemberships
}
