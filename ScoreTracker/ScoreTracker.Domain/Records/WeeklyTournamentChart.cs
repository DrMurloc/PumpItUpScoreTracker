namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record WeeklyTournamentChart(Guid ChartId, DateTimeOffset ExpirationDate)
    {
    }
}
