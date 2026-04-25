namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record BountyLeaderboard(Guid UserId, int MonthlyTotal, int Total)
    {
    }
}
