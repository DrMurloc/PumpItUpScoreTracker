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
    IReadOnlyList<TierListShareCard.Row> Rows,
    IReadOnlyList<TierListShareCard.LegendEntry>? Legend = null)
{
    [ExcludeFromCodeCoverage]
    public sealed record Row(string Name, string ColorHex, IReadOnlyList<Tile> Tiles);

    /// <summary>
    ///     One chart. <paramref name="BadgeHex" /> is the state colour; how it is drawn is
    ///     <paramref name="Outline" /> — the same border language the Compact card uses on the
    ///     page, so a downloaded list looks like the list it came from. <paramref name="Outline" />
    ///     defaults to the dot the card drew before that language existed, so a caller that has
    ///     not been taught the borders renders exactly as it did.
    /// </summary>
    /// <param name="CornerLabel">A printed value in the jacket's bottom-right, e.g. a PUMBILITY gain.</param>
    /// <param name="CornerHex">Its text and outline colour; the caller's accent when null.</param>
    /// <param name="BubbleUrl">
    ///     The difficulty bubble, in the jacket's top-left where the page's card wears it. Null for
    ///     a list that is already one difficulty — a tier list carries its bubble in the header, and
    ///     repeating it on sixty tiles says nothing — and for the charts that have no bubble art.
    /// </param>
    /// <param name="Caption">The song name, in its own band under the jacket. The marks stay on the jacket, above it.</param>
    /// <param name="ScoreLabel">The score as plain preformatted text, bottom-left. Never percentile-colored.</param>
    /// <param name="ScoreMuted">A broken best's score prints muted rather than not at all.</param>
    /// <param name="ExpectedGradeUrl">The expected-grade art riding beside the corner chip (the Expected gains pair).</param>
    /// <param name="Glow">A Perfect Game's halo on the outline — only the grade/plate color modes set it.</param>
    /// <param name="SkillChips">The chart's identity chips, in a band below the caption.</param>
    /// <param name="CompactMarks">
    ///     The settings-era layout: score text left, grade and plate stacked at score height on the
    ///     right, the corner chip outermost. False keeps the original corner-walk layout untouched,
    ///     which is what lets this record grow without moving a pixel of the shipped card.
    /// </param>
    [ExcludeFromCodeCoverage]
    public sealed record Tile(string JacketUrl, string? GradeUrl, string? PlateUrl, string? BadgeHex,
        string? CornerLabel = null, string? CornerHex = null, TileOutline Outline = TileOutline.Dot,
        string? BubbleUrl = null, string? Caption = null, string? ScoreLabel = null, bool ScoreMuted = false,
        string? ExpectedGradeUrl = null, bool Glow = false, IReadOnlyList<SkillChip>? SkillChips = null,
        bool CompactMarks = false);

    /// <summary>One identity chip: its printed words and its family colour, already resolved to hex.</summary>
    [ExcludeFromCodeCoverage]
    public sealed record SkillChip(string Label, string Hex);

    /// <summary>
    ///     One legend row entry — a swatch drawn in <paramref name="Hex" /> with
    ///     <paramref name="Outline" />'s border style, then the label. The card prints a legend
    ///     only for the boundaries actually in the picture, so a shared image explains itself.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record LegendEntry(string Label, string Hex, TileOutline Outline);
}

/// <summary>
///     How a tile's state colour is drawn — the Compact card's own vocabulary: solid for passed,
///     dashed for To-Do and for a pass carried from another mix, dotted for a broken run.
/// </summary>
public enum TileOutline
{
    /// <summary>The corner dot the card drew before it had borders.</summary>
    Dot,
    Solid,
    Dashed,
    Dotted
}
