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
    // The five speed bands, slowest first. All five are claim keys because the chart page and
    // the dialog print the band a chart actually landed in; only the outer two are IDENTITY,
    // which is what keeps "Mid Tempo" off a card. "Mid Tempo" and not the obvious "Moderate":
    // that key is already the comment-moderation button, so the band rendered 관리 ("manage") in
    // Korean and its equivalent elsewhere, and English being the key text is why nothing looked
    // wrong in English.
    public const string VerySlow = "Very Slow";
    public const string Slow = "Slow";
    public const string MidTempo = "Mid Tempo";
    public const string Fast = "Fast";
    public const string VeryFast = "Very Fast";

    /// <summary>The band's rung, 0 (slowest) to 4, or null if the key is not a speed band.</summary>
    public static int? SpeedBandIndex(string badge)
    {
        return badge switch
        {
            VerySlow => 0,
            Slow => 1,
            MidTempo => 2,
            Fast => 3,
            VeryFast => 4,
            _ => null
        };
    }

    /// <summary>Not a shape claim, but keyed the same way: a label the UI localizes, not a badge.</summary>
    public const string LongestRun = "Longest run";

    /// <summary>Whether a chip key is one of the shape claims rather than a piucenter badge.</summary>
    public static bool IsGeometryClaim(string badge)
    {
        return badge is QuarterDouble or HalfDouble or Wide or Twistless or TwistHeavy
            or VeryFast or VerySlow;
    }
}
