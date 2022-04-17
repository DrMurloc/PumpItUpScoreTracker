using ScoreTracker.Domain.Models;

namespace ScoreTracker.Web.Dtos;

public class BestAttemptDto
{
    public string ChartType { get; set; }
    public int Level { get; set; }
    public string SongName { get; set; }
    public string LetterGrade { get; set; } = string.Empty;
    public bool IsBroken { get; set; }
    public int PlayerCount { get; set; }

    public static BestAttemptDto From(BestChartAttempt attempt)
    {
        return new BestAttemptDto
        {
            ChartType = attempt.Chart.Type.ToString(),
            IsBroken = attempt.BestAttempt?.IsBroken ?? true,
            LetterGrade = attempt.BestAttempt?.LetterGrade.ToString() ?? string.Empty,
            SongName = attempt.Chart.SongName,
            Level = attempt.Chart.Level,
            PlayerCount = attempt.Chart.PlayerCount
        };
    }
}