namespace ScoreTracker.EventCompetition.Infrastructure.Entities
{
    internal sealed class QualifierSubmissionDto
    {
        public Guid ChartId { get; set; }
        public int Score { get; set; }
        public string? PhotoUrl { get; set; }
    }
}
