using System.Text.RegularExpressions;

namespace ScoreTracker.Translations.Contracts;

/// <summary>
///     The link-marker convention the pipeline and its callers share. Links never reach the
///     model: the caller lifts each one out and drops a marker in its place — the model is told
///     the markers are links the author placed, and never what they point at — then substitutes
///     the links back into whatever comes home. What makes that safe is verification: a rendering
///     must carry every source marker exactly once, no markers of its own invention, and nothing
///     link-shaped, or it is discarded and the reader sees the original.
///     <para>
///         A marker is <c>⟦1⟧</c>, <c>⟦2⟧</c>, … at level zero, <c>⟦·1⟧</c> at level one, and so
///         on — the level is picked per text so that an author who happens to type a marker-shaped
///         string simply pushes the real markers to a level that provably appears nowhere in their
///         text. Both sides live here because the convention <i>is</i> the contract: the caller
///         builds markers with <see cref="Marker" /> and <see cref="PickLevel" />, the pipeline
///         judges results with <see cref="Violation" />, and neither can drift from the other.
///     </para>
/// </summary>
public static class TranslationMarkers
{
    private static readonly Regex AnyMarker = new(@"⟦(·*)(\d+)⟧", RegexOptions.Compiled);

    /// <summary>
    ///     Anything a browser would treat as a link if it appeared in a rendering. Deliberately
    ///     broader than the caller's URL parser: this is a tripwire for smuggled links, and a
    ///     false positive costs one discarded translation, not a broken comment.
    /// </summary>
    private static readonly Regex LinkShaped = new(@"(?i)(https?://|www\.)", RegexOptions.Compiled);

    public static string Marker(int index, int level)
    {
        return $"⟦{new string('·', level)}{index}⟧";
    }

    /// <summary>
    ///     The smallest level whose opening sequence appears nowhere in <paramref name="text" /> —
    ///     so no marker at that level can collide with anything the author typed.
    /// </summary>
    public static int PickLevel(string text)
    {
        var level = 0;
        while (text.Contains($"⟦{new string('·', level)}", StringComparison.Ordinal)) level++;

        return level;
    }

    /// <summary>The markers in <paramref name="text" />, in order of appearance, duplicates kept.</summary>
    public static IReadOnlyList<string> MarkersIn(string text)
    {
        return AnyMarker.Matches(text).Select(m => m.Value).ToArray();
    }

    /// <summary>
    ///     Why <paramref name="rendering" /> must be discarded, or null when it is safe to keep.
    ///     Judged against <paramref name="source" /> — the marked text that was submitted.
    /// </summary>
    public static string? Violation(string source, string rendering)
    {
        var expected = MarkersIn(source);
        var actual = MarkersIn(rendering);

        if (actual.Count != expected.Count)
            return $"carried {actual.Count} markers where the source has {expected.Count}";

        var missing = expected.Except(actual).FirstOrDefault();
        if (missing != null) return $"lost marker {missing}";

        var counts = actual.GroupBy(m => m).FirstOrDefault(g => g.Count() != expected.Count(e => e == g.Key));
        if (counts != null) return $"repeated marker {counts.Key}";

        if (!ContainsLinkShapedText(source) && ContainsLinkShapedText(rendering))
            return "contains link-shaped text the source does not";

        return null;
    }

    /// <summary>
    ///     Exposed for the caller's own authoritative check — the side that owns a real URL parser
    ///     re-parses after substitution, but a queue-time source with a raw link in it (one the
    ///     caller's parser did not lift) should be caught before money is spent, not after.
    /// </summary>
    public static bool ContainsLinkShapedText(string text)
    {
        return LinkShaped.IsMatch(text);
    }
}
