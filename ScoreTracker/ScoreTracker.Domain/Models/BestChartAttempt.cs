namespace ScoreTracker.Domain.Models;

public sealed record BestChartAttempt(Chart Chart, ChartAttempt? BestAttempt)

{
}