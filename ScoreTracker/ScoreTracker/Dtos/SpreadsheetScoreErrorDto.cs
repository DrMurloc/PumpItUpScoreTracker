namespace ScoreTracker.Web.Dtos;

public class SpreadsheetScoreErrorDto
{
    public string Difficulty { get; set; }
    public string Song { get; set; }
    public string LetterGrade { get; set; }
    public string IsBroken { get; set; }
    public string Error { get; set; }
    public string Score { get; set; }
}