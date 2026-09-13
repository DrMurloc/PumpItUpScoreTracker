using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Web.Services;

/// <summary>
///     How a patch reads on the chart page and in the chart details dialog: the version and,
///     when the catalog knows it, the day it shipped — <c>v2.12.0 · Dec 23, 2025</c>. A version
///     the catalog names rather than numbers (a legacy mix's <c>Release</c>, Infinity's
///     <c>Pre-v1.10</c>) prints as named; only a number takes the <c>v</c>.
/// </summary>
public static class VersionStampText
{
    public static string Short(VersionStamp stamp)
    {
        var version = stamp.Version.Length > 0 && char.IsDigit(stamp.Version[0])
            ? $"v{stamp.Version}"
            : stamp.Version;
        return stamp.ReleaseDate is { } shipped
            ? $"{version} · {shipped.ToString("MMM d, yyyy")}"
            : version;
    }
}
