using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.PlayerProgress.Infrastructure.Entities
{
    [PrimaryKey(nameof(SeasonId), nameof(UserId), nameof(MixId))]
    internal sealed class PlayerStatsEntity
    {
        public Guid UserId { get; set; }
        public Guid MixId { get; set; }

        // 0 = the all-time row, otherwise the season's row (docs/design/seasons.md D11, D34): the
        // same columns computed a second time over the seasonal pool, hidden by the AllTime query
        // filter unless a reader drops it. Leads the key so a season is its own range.
        public short SeasonId { get; set; }

        // The season's TOTAL PUMBILITY: every seasonal best's PUMBILITY summed, the whole-season
        // grind board (docs/design/seasons.md §6.2). Not a pool of fifty, but kept unrounded like
        // the pools because the writer sums doubles. Zero until the season pass writes it (slice
        // 1b), and zero on every all-time row.
        public double TotalPumbility { get; set; }
        public int TotalRating { get; set; }
        public int HighestLevel { get; set; }
        public int ClearCount { get; set; }

        // The four PUMBILITY pools are stored unrounded. A pool is fifty per-chart values that
        // each carry a real fraction, so rounding one of them at rest discards precision the
        // presentation layer is the only thing entitled to spend (docs/UX-GUIDELINES.md).
        public double SkillRating { get; set; }
        public double AverageSkillLevel { get; set; }
        public int AverageSkillScore { get; set; }
        public double SinglesRating { get; set; }
        public double AverageSinglesLevel { get; set; }
        public int AverageSinglesScore { get; set; }
        public double DoublesRating { get; set; }
        public double AverageDoublesLevel { get; set; }
        public int AverageDoublesScore { get; set; }
        public double CoOpRating { get; set; }
        public int AverageCoOpScore { get; set; }
        public double CompetitiveLevel { get; set; }
        public double SinglesCompetitiveLevel { get; set; }
        public double DoublesCompetitiveLevel { get; set; }

        // The Hardmode pools — the same formula over the week's qualifying chart list
        // (docs/design/hardmode-leaderboard.md). Unrounded for the same reason the pools above
        // are, and zero until the census first runs, which is what a mix without Hardmode looks
        // like too. HardmodeChartsHeld rides beside them because "11 of 50" is the sentence the
        // page leads with and it would otherwise cost a second sweep to recover.
        public double HardmodeRating { get; set; }
        public double HardmodeSinglesRating { get; set; }
        public double HardmodeDoublesRating { get; set; }
        // One per pool: three different top-fifties, so a player can hold forty combined and
        // twelve doubles. One shared count printed "50 / 50" on every tab.
        public int HardmodeChartsHeld { get; set; }
        public int HardmodeSinglesChartsHeld { get; set; }
        public int HardmodeDoublesChartsHeld { get; set; }

        // Where the player's PUMBILITY pool would place on the official board, ranked against
        // the last sealed snapshot rather than read back from it — that is what makes the
        // number move on import instead of on the weekly sweep. Named "Estimated" on purpose:
        // an unqualified PumbilityRank reads as authoritative, and it is not. Phoenix fills
        // only the combined one; its site publishes no per-type boards.
        public int? EstimatedPumbilityRank { get; set; }
        public int? EstimatedSinglesPumbilityRank { get; set; }
        public int? EstimatedDoublesPumbilityRank { get; set; }

        /// <summary>The sealed snapshot the estimates were taken against.</summary>
        public DateTimeOffset? PumbilityBoardAsOf { get; set; }
    }
}
