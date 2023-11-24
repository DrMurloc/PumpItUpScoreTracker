namespace ScoreTracker.Domain.Records
{
    public sealed record UserOfficialLeaderboard(string Username, int Place, string OfficialLeaderboardType,
        string LeaderboardName)
    {
    }
}
