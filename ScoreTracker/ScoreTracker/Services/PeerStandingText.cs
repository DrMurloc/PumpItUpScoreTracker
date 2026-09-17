using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The words a standing prints, in one place so the score row, the highlight card, the tier
///     card and the popover cannot drift (docs/design/peers-abstraction.md D10; the D27/D28/D30
///     rules of session-breakdown.md carried over). Standing prints as a PLACE, never a share of
///     the cohort: "#6 of 94 peers" is the same fact as a percentile, reads like a leaderboard,
///     and cannot be read backwards.
/// </summary>
public static class PeerStandingText
{
    /// <summary>
    ///     Null when nothing measured the score — no peers chosen, or none of them has passed the
    ///     chart. The surface then says nothing beside the score; the popover explains on tap.
    /// </summary>
    public static string? Standing(PeerStanding? standing, bool isPerfectGame, IStringLocalizer<App> l)
    {
        if (standing is not { HasCohort: true }) return null;

        // A Perfect Game cannot be beaten, only tied, so every PG row is "#1" and the place stops
        // distinguishing anything. How many peers share it is the fact worth the line instead —
        // unless nobody does, where the place says the better thing.
        if (isPerfectGame && standing.PerfectGames > 0)
            return l["PG · {0} of {1} peers have it", standing.PerfectGames, standing.Passed].Value;

        return l["#{0} of {1} peers", standing.Place, standing.Cohort].Value;
    }

    /// <summary>"You beat 71% · 23 more haven't passed it (5 broke)" — the popover's second line.</summary>
    public static string Summary(PeerStanding standing, IStringLocalizer<App> l)
    {
        var beat = l["You beat {0}%", Math.Round((standing.Percentile ?? 0) * 100)].Value;
        return $"{beat} · {NotPassed(standing, l)}";
    }

    /// <summary>
    ///     "950 to SSS", the number in bold — the points still to go to the next grade; SSS+ counts to a
    ///     Perfect Game. Markup, because the emphasis sits inside a translated sentence, so every
    ///     argument is encoded before it joins it.
    /// </summary>
    public static MarkupString ToNextGrade(GradeProgress progress, IStringLocalizer<App> l)
    {
        var points = $"<b>{WebUtility.HtmlEncode(progress.PointsToNext.ToString("N0"))}</b>";
        return (MarkupString)(progress.NextGrade is { } next
            ? l["{0} to {1} (next grade)", points, WebUtility.HtmlEncode(next.GetName())].Value
            : l["{0} to a Perfect Game", points].Value);
    }

    /// <summary>"81% of the way through SS+" — how much of its current grade the score already covers, rounded down.</summary>
    public static string ThroughGrade(GradeProgress progress, IStringLocalizer<App> l) =>
        l["{0}% of the way through {1}", progress.PointsIntoGrade * 100 / progress.GradeWidth,
            progress.Grade.GetName()].Value;

    public static string NotPassed(PeerStanding standing, IStringLocalizer<App> l)
    {
        if (standing.NotPassed == 0) return l["Every peer has passed it"].Value;
        var line = standing.NotPassed == 1
            ? l["One more hasn't passed it"].Value
            : l["{0} more haven't passed it", standing.NotPassed].Value;
        return standing.Broke > 0 ? $"{line} ({l["{0} broke", standing.Broke].Value})" : line;
    }
}
