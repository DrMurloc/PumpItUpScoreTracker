using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     Splits a piucenter chart key ("Slam_-_Novasonic_S7_ARCADE") into its parts.
///     Handles the INFOBAR marker tokens some keys carry between the level and suffix
///     ("1949_-_SLAM_D28_INFOBAR_TITLE_ARCADE") and the multi-"_-_" song titles
///     ("Wedding_Crashers_-_SHORT_CUT_-_-_SHK_S4_SHORTCUT" — the LAST separator wins).
/// </summary>
internal static partial class PiuCenterKeyParser
{
    /// <summary>
    ///     Keys we refuse to ingest, because the stepchart behind them is not the chart we would
    ///     hang it on. Piucenter's corpus is simfiles, most of them pre-Phoenix, and a few songs
    ///     shipped two charts under one name — XX-era branching paths where only one branch
    ///     survives today.
    ///     <para>
    ///         Baroque Virus FULL D23 is the clearest: v1 reads run / run_without_twists /
    ///         anchor_run, the chart that still exists, while v2 reads hold_footslide / twists /
    ///         bracket — a path nobody can play. Song, type and level cannot tell them apart, so
    ///         the losing keys are named here.
    ///     </para>
    ///     <para>
    ///         Hardcoded on purpose (owner, 2026-08-26): it is a handful of charts in a corpus
    ///         that is winding down, and any rule general enough to catch them would throw away
    ///         good data elsewhere.
    ///     </para>
    /// </summary>
    private static readonly IReadOnlySet<string> RejectedKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Gargoyle_-_FULL_SONG_-_v2_-_Sanxion7_S21_FULLSONG",
            "Baroque_Virus_-_FULL_SONG_-_v2_-_Zircon_D23_INFOBAR_2_FULLSONG"
        };

    /// <summary>Whether this external key names a stepchart we deliberately do not ingest.</summary>
    public static bool IsRejected(string externalKey)
    {
        return RejectedKeys.Contains(externalKey);
    }

    private static readonly Regex KeyPattern = new(
        @"^(?<body>.*)_(?<sl>[SD]\d+)(?:_INFOBAR(?:_[A-Z0-9]+)*)?_(?<suffix>(?:HALFDOUBLE_)?(?:ARCADE|REMIX|SHORTCUT|FULLSONG))$",
        RegexOptions.Compiled);

    public static bool TryParse(string externalKey, out PiuCenterKeyParts parts)
    {
        var match = KeyPattern.Match(externalKey);
        if (!match.Success)
        {
            parts = default!;
            return false;
        }

        var body = match.Groups["body"].Value;
        var separator = body.LastIndexOf("_-_", StringComparison.Ordinal);
        parts = new PiuCenterKeyParts(
            StripVariantMarker(separator < 0 ? body : body[..separator]),
            separator < 0 ? string.Empty : body[(separator + 3)..],
            match.Groups["sl"].Value,
            match.Groups["suffix"].Value);
        return true;
    }

    /// <summary>
    ///     Drop the "_v1" / "_v2" marker piucenter appends when two stepcharts share a song, type
    ///     and level. It sits inside the SONG half of the key, so left alone it normalizes into
    ///     the song name and the key matches nothing at all — which is how Gargoyle FULL SONG S21
    ///     came to keep pre-rejection metrics forever: we refuse its v2 key by name and its v1 key
    ///     could never resolve (field test, 2026-08-26).
    ///     <para>
    ///         Safe against real titles because it requires the underscore-delimited token to be
    ///         exactly "v" and digits: no song in the catalog ends in one, and the whole corpus
    ///         carries four such keys — the two pairs named in <see cref="RejectedKeys" />.
    ///     </para>
    /// </summary>
    private static string StripVariantMarker(string songPart)
    {
        var marker = VariantMarkerPattern().Match(songPart);
        return marker.Success ? songPart[..marker.Index] : songPart;
    }

    [GeneratedRegex(@"_v\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex VariantMarkerPattern();

    /// <summary>
    ///     An artist name with a trailing parenthetical removed — "IVE (아이브)" to "IVE". We store
    ///     the localized name alongside the Latin one and piucenter carries Latin only, and
    ///     <see cref="Normalize" /> cannot bridge them: Hangul and CJK characters ARE letters, so
    ///     they survive the fold and the two sides never meet.
    ///     <para>
    ///         Only ever a FALLBACK key — see the crawl saga's match index. An exact artist always
    ///         wins, so this can rescue a lookup that finds nothing and can never repoint one that
    ///         already resolves.
    ///     </para>
    /// </summary>
    public static string StripTrailingParenthetical(string artist)
    {
        var open = artist.LastIndexOf('(');
        return open > 0 && artist.EndsWith(")", StringComparison.Ordinal)
            ? artist[..open].TrimEnd()
            : artist;
    }

    /// <summary>
    ///     Fold diacritics, drop everything but letters and digits — how a piucenter key's
    ///     song and artist are compared against the catalog's. It lived on the deleted skill
    ///     mapper, which never had anything to do with it; matching keys to charts is this
    ///     file's job.
    /// </summary>
    public static string Normalize(string value)
    {
        var folded = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(folded.Length);
        foreach (var ch in folded)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }
}

[ExcludeFromCodeCoverage]
internal sealed record PiuCenterKeyParts(string SongPart, string ArtistPart, string SordLevel, string Variant);
