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
///     One chip. <see cref="Detail" /> carries the number behind the chip where there is one:
///     measured coverage for <see cref="IdentityChipKind.Core" /> and
///     <see cref="IdentityChipKind.Unique" /> (null for a whole-chart quality, which has no
///     coverage to measure), and peakiness for <see cref="IdentityChipKind.Spike" />. Formatting
///     and localization belong to the reader.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record IdentityChipRecord(
    IdentityChipKind Kind,
    string Badge,
    string DisplayName,
    BadgeCategory? Family,
    decimal? Detail);

public enum IdentityChipKind
{
    /// <summary>A real presence that almost nothing else in this folder has.</summary>
    Unique,

    /// <summary>Far more of this than the folder carries — what the chart is made of.</summary>
    Core,

    /// <summary>The crux runs well over the level the game prints. Carries no badge.</summary>
    Spike,

    /// <summary>What the spike is made of, when that differs from everything above.</summary>
    Crux,

    /// <summary>
    ///     Piucenter's own top-three pick, shown muted when nothing else fired. A chart whose
    ///     coverage is thin everywhere gets an honest "here is what they said" rather than an
    ///     invented distinction.
    /// </summary>
    Fallback
}
