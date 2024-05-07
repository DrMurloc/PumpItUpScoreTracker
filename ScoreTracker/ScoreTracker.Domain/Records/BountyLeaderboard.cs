namespace ScoreTracker.Domain.Records
{
    public sealed record BountyLeaderboard(Guid UserId, int MonthlyTotal, int Total)
    {
    }
}
