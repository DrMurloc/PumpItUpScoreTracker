namespace ScoreTracker.Domain.Records
{
    public sealed record WeeklyTournamentChart(Guid ChartId, DateTimeOffset ExpirationDate)
    {
    }
}
