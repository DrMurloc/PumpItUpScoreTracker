using Microsoft.AspNetCore.Mvc;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     The <c>songType</c> parameter, resolved once for the chart read and the skills read so they
///     cannot disagree: a comma list or a repeated parameter of song cuts, any-of, spelled as a
///     chart row's <c>songType</c> spells them. Every mix shares the four cuts, so unlike a channel
///     there is no per-mix list to check — <c>Remix</c> on a mix without remixes is an empty page,
///     and only a token that is not a cut is a 400.
/// </summary>
internal static class SongTypePicks
{
    public static (IReadOnlySet<SongType>? Picked, ObjectResult? Problem) Resolve(string[]? songTypes,
        Func<string, string, string?, ObjectResult> problem)
    {
        var tokens = songTypes?
            .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();
        if (tokens is null || tokens.Length == 0) return (null, null);

        var picked = new HashSet<SongType>();
        foreach (var token in tokens)
        {
            if (!TryParse(token, out var songType))
                return (null, problem("invalid-song-type", $"'{token}' is not a song type.",
                    $"Valid values: {string.Join(", ", Enum.GetNames<SongType>())}."));
            picked.Add(songType);
        }

        return (picked, null);
    }

    /// <summary>No pick matches everything.</summary>
    public static bool Matches(Chart chart, IReadOnlySet<SongType>? picked)
    {
        return picked is null || picked.Contains(chart.Song.Type);
    }

    /// <summary>The parameter as written, ordered, so the cursor fingerprint carries it.</summary>
    public static string Fingerprint(string[]? songTypes)
    {
        return songTypes is null ? string.Empty : string.Join(",", songTypes.OrderBy(v => v, StringComparer.Ordinal));
    }

    // Name-matched rather than Enum.TryParse, which would also take "1" as ShortCut.
    private static bool TryParse(string token, out SongType songType)
    {
        foreach (var candidate in Enum.GetValues<SongType>())
            if (candidate.ToString().Equals(token, StringComparison.OrdinalIgnoreCase))
            {
                songType = candidate;
                return true;
            }

        songType = default;
        return false;
    }
}
