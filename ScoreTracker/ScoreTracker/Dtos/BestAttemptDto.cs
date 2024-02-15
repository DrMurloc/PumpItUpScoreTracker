using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Web.Dtos;

public class BestAttemptDto
{
    public Guid ChartId { get; set; }
    public string Category { get; set; }
    public int Level { get; set; }
    public string SongName { get; set; }
    public TimeSpan SongDuration { get; set; }
    public string XXLetterGrade { get; set; } = string.Empty;
    public PhoenixLetterGrade? PhoenixLetterGrade { get; set; }
    public PhoenixPlate? PhoenixPlate { get; set; }
    public bool IsBroken { get; set; }
    public string ChartType { get; set; } = string.Empty;
    public string SongType { get; set; } = string.Empty;
    public string SongArtist { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public string DifficultyShorthand { get; set; } = string.Empty;
    public int? Score { get; set; }
    public DateTimeOffset? RecordedOn { get; set; }
    public string VideoUrl { get; set; }

    public string DifficultyBubblePath =>
        $"https://piuimages.arroweclip.se/difficulty/{DifficultyShorthand.ToLower()}.png";

    public static BestAttemptDto From(BestXXChartAttempt attempt)
    {
        return new BestAttemptDto
        {
            ChartId = attempt.Chart.Id,
            SongType = attempt.Chart.Song.Type.ToString(),
            SongDuration = attempt.Chart.Song.Duration,
            Category = attempt.Chart.Type == Domain.Enums.ChartType.CoOp
                ? nameof(Domain.Enums.ChartType.CoOp) + " x" + attempt.Chart.PlayerCount
                : attempt.Chart.Type.ToString(),
            IsBroken = attempt.BestAttempt?.IsBroken ?? true,
            XXLetterGrade = attempt.BestAttempt?.LetterGrade.ToString() ?? string.Empty,
            SongName = attempt.Chart.Song.Name,
            Level = attempt.Chart.Level,
            ChartType = attempt.Chart.Type.ToString(),
            ImagePath = attempt.Chart.Song.ImagePath.ToString(),
            DifficultyShorthand = DifficultyLevel.ToShorthand(attempt.Chart.Type, attempt.Chart.Level),
            Score = attempt.BestAttempt?.Score,
            RecordedOn = attempt.BestAttempt?.RecordedOn
        };
    }
}