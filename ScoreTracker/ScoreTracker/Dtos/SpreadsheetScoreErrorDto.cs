namespace ScoreTracker.Web.Dtos;

public class SpreadsheetScoreErrorDto
{
    public string Difficulty { get; set; } = string.Empty;
    public string Song { get; set; } = string.Empty;
    public string LetterGrade { get; set; } = string.Empty;
    public string IsBroken { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string Score { get; set; } = string.Empty;
    public string Plate { get; set; } = string.Empty;
}