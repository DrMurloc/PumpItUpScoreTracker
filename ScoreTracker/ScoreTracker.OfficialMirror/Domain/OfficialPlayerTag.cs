using System.Text.RegularExpressions;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     Canonical form of an official-site game tag. The site renders the same human two
///     ways — board rows as "TAG#1234", the account page as "TAG #1234" — so every tag
///     crossing an ingest or lookup seam (sweep player upserts, import identity links,
///     profile lookups) collapses to the whitespace-free form before matching or storage.
/// </summary>
internal static class OfficialPlayerTag
{
    // .NET \s spans Unicode whitespace including the U+00A0 non-breaking space that
    // scraped HTML carries as &nbsp;.
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    public static string Normalize(string raw)
    {
        return Whitespace.Replace(raw, "");
    }
}
