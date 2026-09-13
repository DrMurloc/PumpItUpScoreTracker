using MediatR;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     The <c>channel</c> parameter, resolved once for the chart reads and the song read so they
///     cannot disagree (docs/design/song-channels.md §4): a comma list or a repeated parameter of
///     tokens from <c>/api/v2/channels</c>, any-of. A token that is not a channel, or one the mix
///     does not offer — <c>JMusic</c> on Phoenix 2 — is a 400 pointing at the list.
/// </summary>
internal static class ChannelPicks
{
    public static async Task<(IReadOnlySet<Channel>? Picked, ObjectResult? Problem)> Resolve(IMediator mediator,
        MixEnum mix, string[]? channels, Func<string, string, string?, ObjectResult> problem)
    {
        var tokens = channels?
            .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();
        if (tokens is null || tokens.Length == 0) return (null, null);

        var picked = new HashSet<Channel>();
        foreach (var token in tokens)
        {
            if (!ChannelHelperMethods.TryParse(token, out var channel))
                return (null, problem("invalid-channel", $"'{token}' is not a channel.",
                    $"Valid values: {string.Join(", ", Enum.GetNames<Channel>())}."));
            picked.Add(channel);
        }

        var offered = (await mediator.Send(new GetMixChannelsQuery(mix))).Select(c => c.Channel).ToHashSet();
        var missing = picked.FirstOrDefault(c => !offered.Contains(c), (Channel)(-1));
        if ((int)missing >= 0)
            return (null, problem("invalid-channel", $"'{missing}' is not a channel of this mix.",
                $"The channels of {mix} are listed by /api/v2/channels?mix={mix}."));

        return (picked, null);
    }

    /// <summary>A song with no channel on the mix never matches a pick; no pick matches everything.</summary>
    public static bool Matches(Chart chart, IReadOnlySet<Channel>? picked)
    {
        return picked is null || (chart.Song.Channel is { } channel && picked.Contains(channel));
    }

    /// <summary>The parameter as written, ordered, so the cursor fingerprint carries it.</summary>
    public static string Fingerprint(string[]? channels)
    {
        return channels is null ? string.Empty : string.Join(",", channels.OrderBy(v => v, StringComparer.Ordinal));
    }
}
