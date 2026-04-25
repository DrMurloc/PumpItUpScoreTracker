namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record UserOfficialLeaderboard(string Username, int Place, string OfficialLeaderboardType,
        string LeaderboardName, int Score)
    {
    }
}
