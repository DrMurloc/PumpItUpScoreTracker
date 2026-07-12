using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts;

/// <summary>
///     The presentation half of a share-card refresh: the publisher (Web, where
///     MixThemes is the single palette source) resolves the mix palette to raw hexes so
///     the consuming saga stays theme-blind.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record FolderShareCardTheme(
    string BackgroundHex,
    string SurfaceHex,
    string InkHex,
    string InkMutedHex,
    string AccentHex,
    IReadOnlyDictionary<TierListCategory, string> DifficultyHexes);
