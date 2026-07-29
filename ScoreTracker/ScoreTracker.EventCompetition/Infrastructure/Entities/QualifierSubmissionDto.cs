using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.EventCompetition.Infrastructure.Entities
{
    // Serialized into UserQualifierEntity.Entries, which is a plain string column — so new
    // fields here need no migration. Rows written before a field existed deserialize with its
    // default, and From(...) backfills anything that has a knowable answer.
    internal sealed class QualifierSubmissionDto
    {
        public Guid ChartId { get; set; }
        public int Score { get; set; }
        public string? PhotoUrl { get; set; }
        public SubmissionSource? Source { get; set; }
        public DateTimeOffset? SubmittedAt { get; set; }
    }
}
