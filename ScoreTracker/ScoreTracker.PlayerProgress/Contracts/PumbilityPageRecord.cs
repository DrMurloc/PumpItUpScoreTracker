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
/// <param name="Breakdown">
///     Where the pool's total comes from, split three ways. Null only where a caller does not
///     need it — the page always has one, and an empty pool decomposes to zeroes rather than to
///     nothing.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record PumbilityPageRecord(
    MixEnum Mix,
    ChartType? Pool_,
    int Total,
    int? Bar,
    Guid? BarChartId,
    IReadOnlyList<PoolEntry> Pool,
    IReadOnlyList<PoolEntry> WaitingRoom,
    IReadOnlyList<PumbilityTarget> Targets,
    PoolBreakdown? Breakdown = null)
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
///     Where the pool's total comes from: the charts, the scores on them, and the plates walked
///     away with (docs/design/pumbility-overhaul.md §3.6). Summed from
///     <see cref="ScoringConfiguration.Decompose" />, so the parts are the formula's own
///     arithmetic rather than an attribution over it.
/// </summary>
/// <param name="Level">What the fifty charts pay before any score is applied.</param>
/// <param name="FromScore">What the grades add on top. Can be negative below the reference.</param>
/// <param name="FromPlate">
///     What the plates add. Exactly zero on Phoenix, where the plate never entered the formula.
/// </param>
/// <param name="PlateHeadroom">
///     What a perfect plate on every chart in the pool would add — the ceiling on the whole
///     argument, which is the number the section exists to print.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record PoolBreakdown(double Level, double FromScore, double FromPlate, double PlateHeadroom)
{
    public double Total => Level + FromScore + FromPlate;

    /// <summary>Plate floor to plate ceiling: what every plate in the pool is worth end to end.</summary>
    public double PlateSpan => FromPlate + PlateHeadroom;

    /// <summary>Where the held plates sit on that span, or zero where plates pay nothing at all.</summary>
    public double PlateProgress => PlateSpan <= 0 ? 0 : FromPlate / PlateSpan;

    /// <summary>Whether plates are worth anything under this mix's formula at all.</summary>
    public bool PlatesCount => PlateSpan > 0;

    public double ShareOf(double part) => Total <= 0 ? 0 : part / Total;
}

/// <summary>
///     A chart worth playing, with what the player would be projected to score and what that
///     would add.
/// </summary>
/// <param name="Current">
///     What they hold now, or null if they have never scored it. This is what distinguishes an
///     upgrade from something new — the page reads it off the row rather than printing a
///     redundant "kind" column (§3.3).
/// </param>
/// <param name="Source">
///     Where the projected score came from. The distinction is not cosmetic: a
///     <see cref="TargetSource.Phoenix1" /> row is a score the player has already hit, and
///     there is no better evidence than that.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record PumbilityTarget(Guid ChartId, PhoenixScore Projected, int Gain,
    PhoenixScore? Current, bool CurrentIsBroken, TierListCategory? Difficulty,
    TargetSource Source = TargetSource.Peers);

/// <summary>
///     What a projected score is built on.
///     <para>
///         The two are not equally trustworthy and the page must not present them as if they
///         were. <see cref="Peers" /> is an estimate — a quantile of what comparable players
///         scored. <see cref="Phoenix1" /> is the player's own score on that exact chart in the
///         previous mix, repriced: not a guess about what they could do, a record of what they
///         did. It wins wherever both exist, and it is the only signal that works at a mix
///         launch, when there is no peer data to estimate from.
///     </para>
/// </summary>
public enum TargetSource
{
    Peers,
    Phoenix1
}
