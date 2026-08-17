using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.ChartIntelligence.Infrastructure.Entities
{
    // How many of a peer group hold this chart in their top-50 PUMBILITY pool, plus the
    // tier that count banded into (docs/design/pumbility-tier-list.md). Written nightly by
    // the TierListSaga's PUMBILITY rebuild, one row set per folder per peer key.
    //
    // This does not live in TierListEntry because the cards print the peer count, and that
    // table has nowhere to put it. Folder (type, level) is denormalised so a read never joins
    // the catalog — and because a chart's level is per-mix, so ChartId alone does not imply it.
    //
    // PeerKey is the peer group the count was taken over, resolved per mix: a Phoenix 1
    // difficulty title level, a Phoenix 2 PUMBILITY title rung, or PumbilityPeers.Community
    // for everyone at once. Composite key configured in ChartIntelligenceModelContribution.
    internal class PumbilityTierListEntryEntity
    {
        public Guid MixId { get; set; }

        [MaxLength(16)] public string ChartType { get; set; } = string.Empty;

        public int Level { get; set; }

        [MaxLength(64)] public string PeerKey { get; set; } = string.Empty;

        public Guid ChartId { get; set; }

        /// <summary>Peers whose pool contains this chart. Zero is a real answer.</summary>
        public int Appearances { get; set; }

        /// <summary>
        ///     Peers in the group at all, which is what "ranked against N players" reads.
        ///     Constant across a peer key's rows and stored on each so the page never needs a
        ///     second read to say who it ranked you against.
        /// </summary>
        public int PeerCount { get; set; }

        [MaxLength(32)] public string Category { get; set; } = string.Empty;

        public int Order { get; set; }
    }
}
