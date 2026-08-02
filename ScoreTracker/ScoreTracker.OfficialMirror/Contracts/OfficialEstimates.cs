namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>
///     Where a score would sit on a chart's official board — counted against the last sealed
///     snapshot, never read back from it, so it can answer for a score the board has not seen.
///     <para>
///         Estimates, and the type says so. <paramref name="AsOf" /> is the snapshot's date and
///         the boards are swept weekly, so everyone below the player has been improving too:
///         the number leans generous by exactly as much as the week did. Callers print it with
///         a "~" and the date, never bare.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record OfficialPlacementEstimate(int Place, int BoardDepth, DateTimeOffset AsOf);

/// <summary>
///     One official PUMBILITY board's values, highest first, for ranking a pool against.
///     Phoenix publishes a single combined board; Phoenix 2 publishes All, Singles and Doubles.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record OfficialPumbilityBoard(string BoardName, DateTimeOffset AsOf,
    IReadOnlyList<decimal> DescendingValues)
{
    /// <summary>
    ///     The place a pool would take. Ties share the better place — matching the Olympic
    ///     placement the sweep itself writes — and a pool under the last entry ranks one past
    ///     the board's depth, which reads as "outside the top N" rather than as a real seat.
    /// </summary>
    public int PlaceFor(decimal pool)
    {
        var above = 0;
        while (above < DescendingValues.Count && DescendingValues[above] > pool) above++;
        return above + 1;
    }

    public bool IsRanked(decimal pool)
    {
        return PlaceFor(pool) <= DescendingValues.Count;
    }
}

/// <summary>
///     The board names the sweep writes. Phoenix only ever produces <see cref="Combined" /> —
///     its site serves the same list for every <c>?t=</c> tab, so asking it for Singles would
///     store three copies of one board under names the rankings view reads as real per-type
///     boards.
/// </summary>
[ExcludeFromCodeCoverage]
public static class PumbilityBoards
{
    public const string Combined = "PUMBILITY";
    public const string Singles = "PUMBILITY Singles";
    public const string Doubles = "PUMBILITY Doubles";
}
