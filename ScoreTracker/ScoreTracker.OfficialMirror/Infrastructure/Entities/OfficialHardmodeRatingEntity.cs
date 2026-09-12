using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.OfficialMirror.Infrastructure.Entities
{
    // The Official Boards side of the Hardmode leaderboard (docs/design/hardmode-leaderboard.md
    // §4). Keyed by the board player, NOT by a site UserId — the site half lives on PlayerStats,
    // and the two populations are never ranked against each other. Rewritten after every census.
    [PrimaryKey(nameof(OfficialPlayerId), nameof(MixId))]
    internal sealed class OfficialHardmodeRatingEntity
    {
        public int OfficialPlayerId { get; set; }

        public Guid MixId { get; set; }

        // Unrounded, like every other pool the site stores: a pool is fifty fractional
        // contributions and only the presentation layer is entitled to spend that precision.
        public double Combined { get; set; }

        public double Singles { get; set; }

        public double Doubles { get; set; }

        // One per pool, for the same reason the three totals are separate: a board player
        // holding forty charts combined may hold twelve of them on doubles.
        public int ChartsHeld { get; set; }

        public int SinglesChartsHeld { get; set; }

        public int DoublesChartsHeld { get; set; }

        public DateTimeOffset ComputedAt { get; set; }
    }
}
