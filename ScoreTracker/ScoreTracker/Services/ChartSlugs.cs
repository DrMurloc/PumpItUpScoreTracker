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
    public static string SlugifySong(Name songName)
    {
        return Slugify(songName.ToString());
    }

    public static string MixSlug(MixEnum mix)
    {
        return Slugify(mix.GetName());
    }

    public static string DifficultySlug(Chart chart)
    {
        return Slugify(chart.DifficultyString);
    }

    /// <summary>/{mix}/{song}/{difficulty} — the shape the sitemap and in-app links emit.</summary>
    public static string CanonicalPath(Chart chart)
    {
        return $"/{MixSlug(chart.Mix)}/{SlugifySong(chart.Song.Name)}/{DifficultySlug(chart)}";
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
