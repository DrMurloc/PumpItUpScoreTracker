using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record LeaderboardRecord(int Place, Guid UserId, Name UserName, int TotalScore)
    {
    }
}