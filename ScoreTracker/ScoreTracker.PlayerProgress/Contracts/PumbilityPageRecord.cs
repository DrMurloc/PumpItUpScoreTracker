using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     Everything the PUMBILITY page renders, in one read
///     (docs/design/pumbility-overhaul.md §6.2). The hero must not assemble itself from six
///     dispatches — the page's whole argument is that one number and one bar come first.
/// </summary>
/// <param name="Total">The sum of the pool. Zero for a player with no scores yet.</param>
/// <param name="Bar">
///     What the 50th chart is worth — the value anything new has to beat. Null until the pool
///     holds fifty charts, because before that nothing is being displaced.
/// </param>
/// <param name="BarChartId">The chart currently holding the bar, so the page can name it.</param>
/// <param name="Pool">The pool in descending value order, longest first.</param>
/// <param name="WaitingRoom">
///     The charts just outside the pool, best first — what has to cross the line. Capped at six;
///     the curve draws them ghosted.
/// </param>
/// <param name="Targets">What to play next, best gain first.</param>
[ExcludeFromCodeCoverage]
public sealed record PumbilityPageRecord(
    MixEnum Mix,
    ChartType? Pool_,
    int Total,
    int? Bar,
    Guid? BarChartId,
    IReadOnlyList<PoolEntry> Pool,
    IReadOnlyList<PoolEntry> WaitingRoom,
    IReadOnlyList<PumbilityTarget> Targets)
{
    /// <summary>Highest and lowest values in the pool — the curve's read-out.</summary>
    public int PoolTop => Pool.Count == 0 ? 0 : Pool[0].Value;

    public int PoolBottom => Pool.Count == 0 ? 0 : Pool[^1].Value;

    /// <summary>
    ///     How flat the pool is, top to bottom, as a fraction. The one question the pool can
    ///     answer on its own: a flat pool means grinding volume, a steep one means the top
    ///     charts are carrying you.
    /// </summary>
    public double PoolSpread => PoolTop <= 0 ? 0 : (PoolTop - PoolBottom) / (double)PoolTop;

    /// <summary>How many charts sit level with the 50th — the traffic jam at the bar.</summary>
    public int TiedAtBar => Bar == null ? 0 : Pool.Count(p => p.Value == Bar) + WaitingRoom.Count(w => w.Value == Bar);
}

/// <summary>One chart in (or just outside) the pool, with what it is currently worth.</summary>
[ExcludeFromCodeCoverage]
public sealed record PoolEntry(int Place, Guid ChartId, PhoenixScore Score, PhoenixPlate? Plate, bool IsBroken,
    DateTimeOffset RecordedDate, int Value);

/// <summary>
///     A chart worth playing, with what the player would be projected to score and what that
///     would add.
/// </summary>
/// <param name="Current">
///     What they hold now, or null if they have never scored it. This is what distinguishes an
///     upgrade from something new — the page reads it off the row rather than printing a
///     redundant "kind" column (§3.3).
/// </param>
[ExcludeFromCodeCoverage]
public sealed record PumbilityTarget(Guid ChartId, PhoenixScore Projected, int Gain,
    PhoenixScore? Current, bool CurrentIsBroken, TierListCategory? Difficulty, ProjectionEvidence? Evidence);
