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
}

[ExcludeFromCodeCoverage]
internal sealed record PiuCenterKeyParts(string SongPart, string ArtistPart, string SordLevel, string Variant);
