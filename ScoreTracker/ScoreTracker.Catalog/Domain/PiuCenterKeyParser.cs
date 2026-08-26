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
            separator < 0 ? body : body[..separator],
            separator < 0 ? string.Empty : body[(separator + 3)..],
            match.Groups["sl"].Value,
            match.Groups["suffix"].Value);
        return true;
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
