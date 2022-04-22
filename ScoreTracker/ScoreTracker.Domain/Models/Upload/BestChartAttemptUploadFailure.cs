namespace ScoreTracker.Domain.Models.Upload;

public sealed record BestChartAttemptUploadFailure(BestChartAttemptUploadAttempt Attempt, string FailureReason)
{
}