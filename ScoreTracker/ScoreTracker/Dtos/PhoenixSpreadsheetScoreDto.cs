using CsvHelper.Configuration.Attributes;

namespace ScoreTracker.Web.Dtos;

public class PhoenixSpreadsheetScoreDto
{
    public string Difficulty { get; set; }
    public string Song { get; set; }
    public string Score { get; set; }
    public string Plate { get; set; }

    // Read as text so one unreadable cell fails its own row instead of the whole file: CsvHelper
    // converts typed columns inside the record enumerator, outside the extractor's per-row catch.
    [Optional] public string? IsBroken { get; set; }

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