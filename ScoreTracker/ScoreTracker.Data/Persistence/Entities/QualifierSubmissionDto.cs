namespace ScoreTracker.Data.Persistence.Entities
{
    public sealed class QualifierSubmissionDto
    {
        public Guid ChartId { get; set; }
        public int Score { get; set; }
        public string PhotoUrl { get; set; }
    }
}
