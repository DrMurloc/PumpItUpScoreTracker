using System.Globalization;
using System.Text;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.SharedKernel.Caching;

/// <summary>
///     The one place a memory-cache key that names a mix is spelled. Two entry points, because the
///     classification is the decision: a <see cref="Mix" /> key holds a community projection or a
///     catalog fact and must never vary by who is looking; a <see cref="Viewer" /> key holds an
///     object that depends on the viewer's own scores or on chart levels — a player's bests, a
///     stats row, the per-mix chart dictionary, a PUMBILITY projection. They differ by a marker segment
///     so the two can never collide, and a <see cref="Viewer" /> key carries the season as a required
///     parameter — <see cref="SeasonId.AllTime" /> until a seasonal reader exists — so no caller can
///     forget the view (docs/design/seasons.md §4.5, §12.1). A key spelled at
///     a call site instead is what <c>CacheKeyTests</c> exists to catch: one that forgets the view
///     serves one viewer's numbers to the next with no error anywhere.
/// </summary>
public static class CacheKeys
{
    private const string Separator = "__";

    /// <summary>
    ///     A key for something the whole population shares on a mix: a tier list, a cohort, a
    ///     verdict, a folder baseline, a board. Never varies by viewer, by declaration.
    /// </summary>
    public static string Mix(string owner, MixEnum mix, params object?[] parts)
    {
        return Build(owner, null, mix, null, parts);
    }

    /// <summary>
    ///     A key for something that depends on the viewer's own scores or on the levels charts
    ///     carry for them. Same shape as <see cref="Mix" /> plus a marker and the season, so the two can
    ///     never collide, a cache dump says which is which, and a seasonal read never serves an
    ///     all-time entry.
    /// </summary>
    public static string Viewer(string owner, MixEnum mix, SeasonId season, params object?[] parts)
    {
        return Build(owner, "viewer", mix, season, parts);
    }

    /// <summary>
    ///     The same declarations for a caller that holds the mix as its row id rather than the
    ///     enum (the catalog repositories key their dictionaries that way).
    /// </summary>
    public static string Mix(string owner, Guid mixId, params object?[] parts)
    {
        return Build(owner, null, mixId, null, parts);
    }

    public static string Viewer(string owner, Guid mixId, SeasonId season, params object?[] parts)
    {
        return Build(owner, "viewer", mixId, season, parts);
    }

    private static string Build(string owner, string? kind, object mix, SeasonId? season, object?[] parts)
    {
        if (string.IsNullOrWhiteSpace(owner))
            throw new ArgumentException("A cache key names its owner.", nameof(owner));

        var key = new StringBuilder(owner);
        if (kind != null) key.Append(Separator).Append(kind);
        key.Append(Separator).Append(Format(mix));
        if (season != null) key.Append(Separator).Append(Format(season.Value));
        foreach (var part in parts) key.Append(Separator).Append(Format(part));
        return key.ToString();
    }

    private static string Format(object? part)
    {
        return part switch
        {
            null => "",
            string s => s,
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => part.ToString() ?? ""
        };
    }
}
