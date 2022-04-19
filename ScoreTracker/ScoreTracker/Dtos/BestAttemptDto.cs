using ScoreTracker.Domain.Models;

namespace ScoreTracker.Web.Dtos;

public class BestAttemptDto
{
    public string Category { get; set; }
    public int Level { get; set; }
    public string SongName { get; set; }
    public string LetterGrade { get; set; } = string.Empty;
    public bool IsBroken { get; set; }
    public string ChartType { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;

    public static BestAttemptDto From(BestChartAttempt attempt)
    {
        return new BestAttemptDto
        {
            Category = attempt.Chart.Type == Domain.Enums.ChartType.CoOp
                ? nameof(Domain.Enums.ChartType.CoOp) + " x" + attempt.Chart.PlayerCount
                : attempt.Chart.Type.ToString(),
            IsBroken = attempt.BestAttempt?.IsBroken ?? true,
            LetterGrade = attempt.BestAttempt?.LetterGrade.ToString() ?? string.Empty,
            SongName = attempt.Chart.Song.Name,
            Level = attempt.Chart.Level,
            ChartType = attempt.Chart.Type.ToString(),
            ImagePath = attempt.Chart.Song.ImagePath.ToString()
        };
    }
}