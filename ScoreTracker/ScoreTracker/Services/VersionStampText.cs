using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Web.Services;

/// <summary>
///     How a patch reads on the chart page and in the chart details dialog. A numbered version
///     takes the <c>v</c>: <c>v2.12.0</c>. A version the catalog names rather than numbers — a legacy
///     mix's <c>Release</c>, Infinity's <c>Pre-v1.10</c> — is the mix's own release, and the mix and
///     the date already say that, so it prints nothing on its own.
/// </summary>
public static class VersionStampText
{
    /// <summary>The patch, <c>v2.12.0</c>, or null for a version the catalog only names.</summary>
    public static string? Patch(VersionStamp stamp)
    {
        return stamp.Version.Length > 0 && char.IsDigit(stamp.Version[0]) ? $"v{stamp.Version}" : null;
    }

    /// <summary>The day a patch shipped, <c>Dec 23, 2025</c>.</summary>
    public static string Day(DateOnly shipped)
    {
        return shipped.ToString("MMM d, yyyy");
    }

    /// <summary>
    ///     The patch and, when known, the day it shipped: <c>v2.12.0 · Dec 23, 2025</c>. A named
    ///     version leaves the day alone, and names itself only when there is no day to give.
    /// </summary>
    public static string Short(VersionStamp stamp)
    {
        var patch = Patch(stamp);
        return (patch, stamp.ReleaseDate) switch
        {
            ({ } p, { } shipped) => $"{p} · {Day(shipped)}",
            ({ } p, null) => p,
            (null, { } shipped) => Day(shipped),
            _ => stamp.Version
        };
    }
}
