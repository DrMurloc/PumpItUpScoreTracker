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
///     What lights the glow (D15/D16). One rule, one strength — the glow says a score crossed the
///     line the player set; the color is the spectrum. Off switches off Perfect Games too.
/// </summary>
public enum GlowRule
{
    PerfectGames,
    TopPlaces,
    TopPercent,
    Off
}

/// <summary>
///     The color system and glow rule a player chose on <c>/Account</c>, stored as the
///     <see cref="SettingKey" /> UI setting. Packed like ShareCardOptions: a version token and
///     named fields, unknown ones ignored, so a rolled-back release can read a newer save.
/// </summary>
public sealed record ScoreColorSettings(ScoreColorSystem System, GlowRule Glow, int GlowThreshold)
{
    public const string SettingKey = "Universal__ScoreColors";

    private const string Version = "v1";
    private const int DefaultPercent = 10;
    private const int DefaultPlaces = 1;
    private const int MaxThreshold = 50;

    /// <summary>Today's page: the judgement spectrum, glowing from the top 10% (D20).</summary>
    public static ScoreColorSettings Default { get; } = new(ScoreColorSystem.JudgementSpectrum, GlowRule.TopPercent,
        DefaultPercent);

    /// <summary>The systems that read a standing at all; the other two paint without one.</summary>
    public bool UsesStanding => System is not (ScoreColorSystem.ActualGrade or ScoreColorSystem.None);

    public string Serialize() => $"{Version},system={System},glow={Glow},threshold={GlowThreshold}";

    public static ScoreColorSettings Parse(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return Default;
        var tokens = stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!tokens.Contains(Version, StringComparer.OrdinalIgnoreCase)) return Default;

        var system = Default.System;
        var glow = Default.Glow;
        int? threshold = null;
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
        }

        return new ScoreColorSettings(system, glow, Clamp(glow, threshold));
    }

    /// <summary>A threshold that means something for the rule: places and percents both live in 1–50.</summary>
    public static int Clamp(GlowRule glow, int? threshold)
    {
        var fallback = glow == GlowRule.TopPlaces ? DefaultPlaces : DefaultPercent;
        return Math.Clamp(threshold ?? fallback, 1, MaxThreshold);
    }
}
