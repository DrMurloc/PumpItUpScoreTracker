namespace ScoreTracker.Domain.Models;

public sealed record BestXXChartAttempt(Chart Chart, XXChartAttempt? BestAttempt)
{
}