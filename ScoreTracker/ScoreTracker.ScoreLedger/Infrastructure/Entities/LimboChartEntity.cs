namespace ScoreTracker.ScoreLedger.Infrastructure.Entities;

// The charts that carry a limbo leaderboard — the "how low can you clear it" board, ranked
// ascending on lowest passing score (docs/design/limbo-leaderboard.md D1). Rows are inserted by
// hand-run SQL: there is no admin screen and nothing derives them, so the application only ever
// reads this table. Presence is the whole record — a flagged chart shows the chip, an unflagged
// one does not, and nothing here changes what the ledger keeps.
internal sealed class LimboChartEntity
{
    public Guid ChartId { get; set; }

    public Guid MixId { get; set; }
}
