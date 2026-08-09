namespace ScoreTracker.Web.Services;

/// <summary>
///     How a PUMBILITY figure is written down. Presentation is the only layer allowed to round
///     one at all, and this is where that rounding lives, so a number reads the same wherever
///     it appears (docs/UX-GUIDELINES.md).
///     <para>
///         A <b>total</b> is always two decimals — <c>ToString("N2")</c> at the call site,
///         matching what piugame prints and what our mirror of its board has always rendered.
///         There is no helper for that because there is nothing to decide.
///     </para>
///     <para>
///         A <b>gain</b> is different. Gains render in compact places — a corner badge on a
///         chart card, a chip on a session row — where "+137.00" is four characters of noise
///         around one fact. So a gain shows only as much precision as it needs, and never more:
///     </para>
///     <list type="bullet">
///         <item><description>10 or more — whole, "+137"</description></item>
///         <item><description>1 to 10 — one decimal, "+9.4"</description></item>
///         <item><description>under 1 — two decimals, "+0.42"</description></item>
///     </list>
///     <para>
///         Truncated, never rounded, at every rung. A gain that reads higher than it was is a
///         promise the pool did not keep, and the same argument holds on the losing side, which
///         is why the truncation is applied to the magnitude rather than the signed value —
///         <c>Math.Floor(-0.5)</c> is -1, and that overstates a loss.
///     </para>
/// </summary>
public static class PumbilityFormat
{
    /// <summary>
    ///     Where float noise is cleaned off before anything truncates. A gain that is really
    ///     0.40 arrives from a chain of double arithmetic as 0.3999999999999773, and truncating
    ///     that directly prints "0.39" — a wrong number produced by the fix for wrong numbers.
    ///     Six places is far below the coarsest rung and far above the noise.
    /// </summary>
    private const int NoiseFloor = 6;

    /// <summary>
    ///     One per rung, so the scale a magnitude is truncated against is a literal rather than
    ///     a <see cref="Math.Pow" /> result. See <see cref="Truncated" /> for why that matters.
    /// </summary>
    private static readonly decimal[] Scales = { 1m, 10m, 100m };

    /// <summary>
    ///     A gain's magnitude, unsigned. Callers own the sign or the arrow — they do not agree
    ///     on which ("+137" on a badge, "▲137" on a board), and that is a presentation choice
    ///     per surface rather than part of the number.
    ///     <para>
    ///         The noise comes off in <c>double</c>, where it was created, and the rung is chosen
    ///         and applied in <c>decimal</c>. Converting after the round is lossless for what
    ///         survives it: double-to-decimal is itself a round to fifteen significant digits, and
    ///         a six-place value is well inside that.
    ///     </para>
    /// </summary>
    public static string Gain(double value)
    {
        return Gain((decimal)Math.Round(Math.Abs(value), NoiseFloor, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    ///     The mirrored official surfaces carry decimals, because the board they quote does. Those
    ///     arrive already exact at two places, so they skip the noise floor rather than round-trip
    ///     through a double that could only lose to it.
    /// </summary>
    public static string Gain(decimal value)
    {
        var magnitude = Math.Abs(value);
        return magnitude >= 10 ? Truncated(magnitude, 0)
            : magnitude >= 1 ? Truncated(magnitude, 1)
            : Truncated(magnitude, 2);
    }

    /// <summary>
    ///     Truncation happens in decimal because scaling by a power of ten is exact there and is
    ///     not in binary floating point. The same step in doubles read a gain of 0.29 as "0.28":
    ///     the nearest double to 0.29 times 100 is 28.999999999999996, and truncating that loses
    ///     the hundredth the rung exists to show. Cleaning the noise first does not save it —
    ///     0.29 IS the cleaned value, and the error is reintroduced by the multiply after.
    /// </summary>
    private static string Truncated(decimal magnitude, int decimals)
    {
        var scale = Scales[decimals];
        return (Math.Truncate(magnitude * scale) / scale).ToString("N" + decimals);
    }
}
