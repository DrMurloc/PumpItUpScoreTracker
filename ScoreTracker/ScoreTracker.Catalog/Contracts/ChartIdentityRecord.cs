namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     What a chart is, said relative to the folder it sits in
///     (docs/design/chart-identity.md §3). Chips arrive in display order and every surface
///     renders the same list — the tier lists, the chart page and its dialog cannot disagree
///     about a chart because there is only one answer to ask for.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartIdentityRecord(Guid ChartId, IReadOnlyList<IdentityChipRecord> Chips);

/// <summary>
///     One chip. <see cref="Detail" /> carries the number behind it where there is one: measured
///     coverage for a badge claim (null for a whole-chart quality, which has no coverage),
///     peakiness for <see cref="IdentityChipKind.Spike" />, seconds for
///     <see cref="IdentityChipKind.HardSection" />, and the geometry share for the width and
///     twist claims. Formatting and localization belong to the reader.
///     <para>
///         <see cref="Badges" /> is populated only for the hard-section chip, which names two
///         badges at once — one window, so printing its length twice was printing the same
///         number twice. Every other kind leaves it empty and uses <see cref="Badge" />.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record IdentityChipRecord(
    IdentityChipKind Kind,
    IdentityTier Tier,
    string Badge,
    string DisplayName,
    BadgeCategory? Family,
    decimal? Detail,
    IReadOnlyList<IdentityChipBadge>? Badges = null);

/// <summary>One badge inside a chip that names more than one, keeping its own family colour.</summary>
[ExcludeFromCodeCoverage]
public sealed record IdentityChipBadge(string Badge, string DisplayName, BadgeCategory? Family);

/// <summary>
///     Whether a chip says what the chart IS or merely what it also has
///     (docs/design/chart-identity.md §1). The distinction is the feature: a half-double that
///     features bracket twists is a different chart from a bracket-twist chart that features
///     mid-6 patterns, and a flat chip row cannot tell you which one you are looking at.
/// </summary>
public enum IdentityTier
{
    /// <summary>What the chart is. Rendered loud, first, and never capped.</summary>
    Identity,

    /// <summary>What it also has. Allowed to be ordinary — most things are.</summary>
    Feature
}

public enum IdentityChipKind
{
    /// <summary>How much of the pad the chart uses: quarter-double, half-double, or wide.</summary>
    Width,

    /// <summary>How far the chart turns you: twist-heavy, or twistless.</summary>
    Twist,

    /// <summary>Only ever the outer bands — a chart in the middle of its folder has no speed claim.</summary>
    Speed,

    /// <summary>A real presence that almost nothing else in this folder has.</summary>
    Unique,

    /// <summary>Far more of this than the folder carries — what the chart is made of.</summary>
    Core,

    /// <summary>The crux runs well over the level the game prints. Carries no badge.</summary>
    Spike,

    /// <summary>
    ///     What the chart's hardest stretch is made of, and how long that stretch runs. A
    ///     separate claim from <see cref="Spike" />, which is about elevation rather than
    ///     composition: most charts are flat and have no spike but still have a hardest part.
    /// </summary>
    HardSection,

    /// <summary>
    ///     Piucenter's own top-three pick, shown muted when nothing else fired. A chart whose
    ///     coverage is thin everywhere gets an honest "here is what they said" rather than an
    ///     invented distinction.
    /// </summary>
    Fallback
}
