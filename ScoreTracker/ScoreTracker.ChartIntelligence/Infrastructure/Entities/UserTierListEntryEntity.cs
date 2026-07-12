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

        // Score-age workshop: how loudly this entry votes in the similar-players
        // aggregation (1 = full voice). Computed at materialization from the score's
        // age relative to the player's OWN ages within the folder — a uniformly-old
        // folder is a coherent snapshot and keeps 1.0 everywhere; only era-mixed
        // entries fade.
        public double Freshness { get; set; } = 1.0;
    }
}
