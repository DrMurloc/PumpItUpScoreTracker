namespace ScoreTracker.ChartIntelligence.Infrastructure.Entities
{
    // One user's materialized relative tier list category for one chart. Composite key
    // (MixId, UserId, ChartId) and the folder-read covering index are configured in
    // ChartIntelligenceModelContribution.
    internal class UserTierListEntryEntity
    {
        public Guid MixId { get; set; }
        public Guid UserId { get; set; }
        public Guid ChartId { get; set; }
        public string Category { get; set; } = string.Empty;
        public int Order { get; set; }
    }
}
