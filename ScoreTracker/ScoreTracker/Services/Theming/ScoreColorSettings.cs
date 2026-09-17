namespace ScoreTracker.Web.Services.Theming;

/// <summary>
///     How a player's own scores are painted (docs/design/peers-abstraction.md D14). Seven of these
///     read the peer standing; two ignore it and paint the score's own grade, or nothing.
/// </summary>
public enum ScoreColorSystem
{
    /// <summary>Today's rarity ramp: grey → silver → green → gold → ice → prism. The default.</summary>
    JudgementSpectrum,

    /// <summary>The retired Raider.io-style ladder, hues retuned: grey → white → green → blue → purple → orange → pink.</summary>
    Classic,

    /// <summary>The grades' own metals by standing: below-A green → copper → silver → gold → SSS ice → SSS+ at the top 1%.</summary>
    GradeMetals,

    /// <summary>Medals for a place, not a share: gold #1, silver #2, copper #3, plain below.</summary>
    Podium,

    /// <summary>The mix primary from dark to bright — ordered by lightness alone.</summary>
    SingleHue,

    /// <summary>The judgement colors literally, Miss red at the bottom. Opt-in only.</summary>
    ResultScreen,

    /// <summary>Plain below the middle, gold above, ice for the top 10%.</summary>
    ThreeSteps,

    /// <summary>The score wears its own grade's metal; peers show only as text.</summary>
    ActualGrade,

    /// <summary>Plain ink; peers show only as text.</summary>
    None
}

/// <summary>
///     What lights the glow (D15/D16, D39). One rule at a time. The peer rules say a score crossed
///     the line the player set; the two grade rules measure a score against its next letter grade
///     and need no peers. Off switches off Perfect Games too.
/// </summary>
public enum GlowRule
{
    PerfectGames,
    TopPlaces,
    TopPercent,
    Off,

    /// <summary>Lit when a score is strictly under the threshold's points from its next grade.</summary>
    UnderPointsToNextGrade,

    /// <summary>Lit when a score is in the last threshold percent of its grade.</summary>
    LastPercentOfGrade
}

/// <summary>How a grade rule lights a score (D40). The peer rules always wear one glow.</summary>
public enum GlowStrength
{
    One,

    /// <summary>Faint where the rule's window opens, full at the next grade.</summary>
    BrighterTheCloser
}

/// <summary>
///     The color system and glow rule a player chose on <c>/Account</c>, stored as the
///     <see cref="SettingKey" /> UI setting. Packed like ShareCardOptions: a version token and
///     named fields, unknown ones ignored, so a rolled-back release can read a newer save.
/// </summary>
public sealed record ScoreColorSettings(
    ScoreColorSystem System,
    GlowRule Glow,
    int GlowThreshold,
    GlowStrength Strength = GlowStrength.One)
{
    public const string SettingKey = "Universal__ScoreColors";

    public const int DefaultPlaces = 1;
    public const int DefaultPercent = 10;
    public const int MaxPeerThreshold = 50;
    public const int DefaultPoints = 1000;
    public const int MaxPoints = 5000;
    public const int DefaultGradePercent = 20;
    public const int MaxGradePercent = 100;

    private const string Version = "v1";

    /// <summary>Today's page: the judgement spectrum, glowing from the top 10% (D20).</summary>
    public static ScoreColorSettings Default { get; } = new(ScoreColorSystem.JudgementSpectrum, GlowRule.TopPercent,
        DefaultPercent);

    /// <summary>The systems that read a standing at all; the other two paint without one.</summary>
    public bool UsesStanding => System is not (ScoreColorSystem.ActualGrade or ScoreColorSystem.None);

    /// <summary>The rule measures a score against its next grade rather than its peers.</summary>
    public bool IsGradeRule => IsGradeRuleFor(Glow);

    public static bool IsGradeRuleFor(GlowRule glow) =>
        glow is GlowRule.UnderPointsToNextGrade or GlowRule.LastPercentOfGrade;

    /// <summary>
    ///     A grade rule's number is written under its own field. A release that predates the grade
    ///     rules cannot read the rule, falls back to the default one, and finds no threshold of its
    ///     own to misread as a percentage of peers.
    /// </summary>
    public string Serialize() =>
        $"{Version},system={System},glow={Glow},{(IsGradeRule ? "near" : "threshold")}={GlowThreshold},strength={Strength}";

    public static ScoreColorSettings Parse(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return Default;
        var tokens = stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!tokens.Contains(Version, StringComparer.OrdinalIgnoreCase)) return Default;

        var system = Default.System;
        var glow = Default.Glow;
        var strength = Default.Strength;
        int? threshold = null;
        int? near = null;
        foreach (var token in tokens)
        {
            var split = token.IndexOf('=');
            if (split <= 0) continue;
            var key = token[..split];
            var value = token[(split + 1)..];
            if (key.Equals("system", StringComparison.OrdinalIgnoreCase) &&
                Enum.TryParse<ScoreColorSystem>(value, true, out var parsedSystem))
                system = parsedSystem;
            else if (key.Equals("glow", StringComparison.OrdinalIgnoreCase) &&
                     Enum.TryParse<GlowRule>(value, true, out var parsedGlow))
                glow = parsedGlow;
            else if (key.Equals("threshold", StringComparison.OrdinalIgnoreCase) &&
                     int.TryParse(value, out var parsedThreshold))
                threshold = parsedThreshold;
            else if (key.Equals("near", StringComparison.OrdinalIgnoreCase) &&
                     int.TryParse(value, out var parsedNear))
                near = parsedNear;
            else if (key.Equals("strength", StringComparison.OrdinalIgnoreCase) &&
                     Enum.TryParse<GlowStrength>(value, true, out var parsedStrength))
                strength = parsedStrength;
        }

        return new ScoreColorSettings(system, glow, Clamp(glow, IsGradeRuleFor(glow) ? near : threshold), strength);
    }

    /// <summary>
    ///     A threshold that means something for the rule: places and percents of peers live in 1–50,
    ///     points to the next grade in 1–5,000, and a share of a grade in 1–100.
    /// </summary>
    public static int Clamp(GlowRule glow, int? threshold)
    {
        var (fallback, max) = glow switch
        {
            GlowRule.TopPlaces => (DefaultPlaces, MaxPeerThreshold),
            GlowRule.UnderPointsToNextGrade => (DefaultPoints, MaxPoints),
            GlowRule.LastPercentOfGrade => (DefaultGradePercent, MaxGradePercent),
            _ => (DefaultPercent, MaxPeerThreshold)
        };
        return Math.Clamp(threshold ?? fallback, 1, max);
    }
}
