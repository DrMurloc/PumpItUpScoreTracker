namespace ScoreTracker.Domain.Records;

/// <summary>
///     Everything the share-card renderer needs, resolved to raw values — the renderer
///     stays theme-blind (colors arrive as hexes the caller resolved from the mix
///     palette) and layout-only. One model serves both consumers: the tier-list page's
///     Download button and the per-folder og:image job (design doc §7).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record TierListShareCard(
    string Title,
    string Subtitle,
    string Stamp,
    string AccentHex,
    string BackgroundHex,
    string SurfaceHex,
    string InkHex,
    string InkMutedHex,
    string LinkUrl,
    string? BubbleUrl,
    IReadOnlyList<TierListShareCard.Row> Rows)
{
    [ExcludeFromCodeCoverage]
    public sealed record Row(string Name, string ColorHex, IReadOnlyList<Tile> Tiles);

    [ExcludeFromCodeCoverage]
    public sealed record Tile(string JacketUrl, string? GradeUrl, string? PlateUrl, string? BadgeHex);
}
