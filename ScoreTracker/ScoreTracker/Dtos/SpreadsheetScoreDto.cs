using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Web.Dtos;

public class SpreadsheetScoreDto
{
    public string Difficulty { get; set; }
    public string Song { get; set; }
    public string LetterGrade { get; set; }

    public SpreadsheetScoreErrorDto ToError(string errorReason)
    {
        return new()
        {
            Difficulty = Difficulty,
            Song = Song,
            LetterGrade = LetterGrade,
            Error = errorReason
        };
    }

    public BestChartAttempt ToBestAttempt()
    {
        var (chartType, level) = DifficultyLevel.ParseShortHand(Difficulty);

        return new BestChartAttempt(
            new Chart(new Song(Song, new Uri("/", UriKind.Relative)), chartType, level),
            string.IsNullOrWhiteSpace(LetterGrade)
                ? null
                : new ChartAttempt(Enum.Parse<LetterGrade>(LetterGrade, true), false));
    }
}