namespace ScoreTracker.Web.Dtos;

public class PhoenixSpreadsheetScoreDto
{
    public string Difficulty { get; set; }
    public string Song { get; set; }
    public string Score { get; set; }
    public string Plate { get; set; }

    public SpreadsheetScoreErrorDto ToError(string errorReason)
    {
        return new SpreadsheetScoreErrorDto
        {
            Difficulty = Difficulty,
            Song = Song,
            Error = errorReason,
            Score = Score,
            Plate = Plate
        };
    }
}