using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     Everything the Hardmode tab renders for one viewer, in one read
///     (docs/design/hardmode-leaderboard.md). Same shape of promise the PUMBILITY page makes:
///     the number and the pool come together, because the page's argument is the number.
/// </summary>
/// <param name="Combined">The pool across both types — the board's headline number.</param>
/// <param name="Pool">The selected pool's charts in descending value order.</param>
/// <param name="ScoredOutsidePool">
///     Qualifying charts the viewer has a score on that the fifty does not hold — the third state
///     the chart list draws. Empty while the pool is short of fifty, because then nothing is
///     displaced (design §7).
/// </param>
/// <param name="Rails">The Phoenix 2 ladders asked of the Hardmode pool.</param>
/// <param name="QualifyingCharts">How many charts the week's list holds, so the page can say so.</param>
[ExcludeFromCodeCoverage]
public sealed record HardmodePageRecord(
    MixEnum Mix,
    ChartType? Pool_,
    HardmodePoolTotals Combined,
    HardmodePoolTotals Singles,
    HardmodePoolTotals Doubles,
    IReadOnlyList<PoolEntry> Pool,
    IReadOnlyList<PoolEntry> ScoredOutsidePool,
    IReadOnlyList<TitleRail> Rails,
    int QualifyingCharts);

/// <summary>
///     One pool's standing. <paramref name="Rank" /> and <paramref name="Field" /> are null until
///     the census has run, which is also what a mix without Hardmode looks like.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record HardmodePoolTotals(double Total, int Held, int? Rank, int? Field)
{
    public static HardmodePoolTotals Empty { get; } = new(0, 0, null, null);
}
