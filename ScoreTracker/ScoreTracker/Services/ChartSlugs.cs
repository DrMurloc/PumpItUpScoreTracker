using System.Text;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Deterministic vanity-URL slugs for the chart-page canonical lattice
///     (docs/design/chart-details-overhaul.md): lowercase, hyphens, URL-hostile
///     punctuation stripped, and unicode preserved — Korean titles stay Korean, which is
///     actively better for Korean search. Slugs are derived, never stored: the same chart
///     record always yields the same path, so in-app links build canonicals directly and
///     the 301 endpoints only serve external/legacy URLs (Blazor's link interception
///     would eat an in-app hop through MVC).
/// </summary>
public static class ChartSlugs
{
    // Names made entirely of URL-hostile punctuation slugify to nothing — they get a
    // hand-picked stable slug instead ("!" is the only one in the catalog).
    private static readonly IReadOnlyDictionary<string, string> UnslugifiableNames =
        new Dictionary<string, string>
        {
            ["!"] = "exclamation"
        };

    public static string SlugifySong(Name songName)
    {
        var raw = songName.ToString();
        var slug = Slugify(raw);
        if (slug.Length > 0) return slug;
        return UnslugifiableNames.TryGetValue(raw, out var named) ? named : "untitled";
    }

    public static string MixSlug(MixEnum mix)
    {
        return Slugify(mix.GetName());
    }

    /// <summary>
    ///     Slot-aware: pre-Exceed slots are identity (the same song can carry Hard 6 AND
    ///     Crazy 6, and "s6" alone is ambiguous there), so slotted charts slug from
    ///     DifficultyDisplay ("crazy-6") while slotless ones keep the shorthand ("d20").
    /// </summary>
    public static string DifficultySlug(Chart chart)
    {
        return Slugify(chart.Slot != null ? chart.DifficultyDisplay : chart.DifficultyString);
    }

    /// <summary>/Charts/{mix}/{song}/{difficulty} — the shape the sitemap and in-app links emit.</summary>
    public static string CanonicalPath(this Chart chart)
    {
        return $"/Charts/{MixSlug(chart.Mix)}/{SlugifySong(chart.Song.Name)}/{DifficultySlug(chart)}";
    }

    private static string Slugify(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingHyphen = false;
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingHyphen && builder.Length > 0) builder.Append('-');
                pendingHyphen = false;
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (char.IsWhiteSpace(character) || character is '-' or '_')
            {
                pendingHyphen = true;
            }
            // Everything else (apostrophes, dots, question marks, …) drops silently —
            // "Why Don't You Get Up and Dance, Man?" → why-dont-you-get-up-and-dance-man.
        }

        return builder.ToString();
    }
}
