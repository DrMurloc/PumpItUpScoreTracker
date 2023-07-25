namespace ScoreTracker.Web.Dtos;

public class XXSpreadsheetScoreDto
{
    public string Difficulty { get; set; }
    public string Song { get; set; }
    public string LetterGrade { get; set; }
    public string IsBroken { get; set; }
    public string Score { get; set; }

    public SpreadsheetScoreErrorDto ToError(string errorReason)
    {
        return new SpreadsheetScoreErrorDto
        {
            Difficulty = Difficulty,
            Song = Song,
            LetterGrade = LetterGrade,
            Error = errorReason,
            IsBroken = IsBroken,
            Score = Score
        };
    }
}