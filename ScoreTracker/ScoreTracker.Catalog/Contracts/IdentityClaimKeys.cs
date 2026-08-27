namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     The claim keys that are not piucenter badges — the chart's shape, its speed band, and the
///     length of its longest run (docs/design/chart-identity.md §3.2, §3.8). They belong to no
///     badge family and carry no coverage, but they travel as chips, so they need stable keys.
///     <para>
///         Public because the reader has to tell them apart to paint them: a width claim wears
///         Doubles Tech green, Longest run wears Stamina &amp; Runs red, and the two speed claims
///         wear opposite ends of the speed ramp. Matching on the English display text at the call
///         site would make that colouring a typo away from silently wrong.
///     </para>
/// </summary>
public static class IdentityClaimKeys
{
    public const string QuarterDouble = "Quarter Double";
    public const string HalfDouble = "Half-Double";
    public const string Wide = "Wide";
    public const string Twistless = "Twistless";
    public const string TwistHeavy = "Twist-heavy";
    public const string VeryFast = "Very Fast";
    public const string VerySlow = "Very Slow";

    /// <summary>Not a shape claim, but keyed the same way: a label the UI localizes, not a badge.</summary>
    public const string LongestRun = "Longest run";

    /// <summary>Whether a chip key is one of the shape claims rather than a piucenter badge.</summary>
    public static bool IsGeometryClaim(string badge)
    {
        return badge is QuarterDouble or HalfDouble or Wide or Twistless or TwistHeavy
            or VeryFast or VerySlow;
    }
}
