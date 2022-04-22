using ScoreTracker.Domain.Models.Upload;

namespace ScoreTracker.Web.Dtos;

public class SpreadsheetScoreDto
{
    public string Difficulty { get; set; }
    public string Song { get; set; }
    public string LetterGrade { get; set; }

    public BestChartAttemptUploadFailure ToError(string errorReason)
    {
        return new BestChartAttemptUploadFailure(new BestChartAttemptUploadAttempt(Song, Difficulty, LetterGrade),
            errorReason);
    }

    public BestChartAttemptUploadAttempt ToUploadAttempt()
    {
        return new BestChartAttemptUploadAttempt(Song, Difficulty, LetterGrade);
    }
}