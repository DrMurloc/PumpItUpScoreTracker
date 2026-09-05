using Microsoft.Extensions.Localization;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Web.Components;

/// <summary>
///     What capture learned about one play, as the marks the page draws for it — shared by the
///     score row and the highlight card so one play cannot wear different medals on different
///     surfaces. The gain is the one mark the surfaces draw differently (a small badge on a
///     row, the established chip on a card), so it is opt-in here.
/// </summary>
internal static class SessionBadges
{
    /// <summary>One item in the strip. Most are a glyph and an optional suffix; two are numbers that
    /// elaborate the glyph beside them and carry their own treatment.</summary>
    internal sealed record SessionBadge(string Text, string Tooltip, string Class = "sbd-badge");

    private const int PerfectGame = 1_000_000;

    /// <summary>
    ///     Under a twentieth of a level the competitive difference is recomputation noise
    ///     wearing a plus sign, so it says nothing rather than "+0.0".
    /// </summary>
    private const double MinimumCompetitiveGain = 0.05;

    /// <summary>
    ///     <paramref name="includePhoenix1" /> is the card's opt-out: there the "+N over P1"
    ///     fact reads as foot text beside the standing, not as an art badge (field test).
    /// </summary>
    internal static IEnumerable<SessionBadge> For(SessionScore score, IStringLocalizer<App> l,
        bool includeGain, bool includePhoenix1 = true)
    {
        var detail = score.Detail;
        if (score.Flags.HasFlag(HighlightFlags.PumbilityTop50))
            yield return new SessionBadge(detail?.PumbilityRank is { } r ? $"👑 #{r}" : "👑",
                l["In your PUMBILITY top 50"].Value);
        // Straight after the crown, because it answers what the crown does not: the crown
        // says the chart sits in your pool, which it may have done all night. This says what
        // tonight's play on it was worth.
        if (includeGain && detail?.PumbilityGain is { } gain && gain > 0)
            yield return new SessionBadge($"+{PumbilityFormat.Gain(gain)}",
                l["What this play added to your PUMBILITY"].Value, "sbd-gain");
        // No ScoreQuality90 badge: it said "top 10% among comparable players", and the
        // standing line beneath now says exactly where you placed among them. The flag
        // itself is untouched — it still rides the Discord card and seeds hot streaks.
        if (score.Flags.HasFlag(HighlightFlags.FolderCompletion90))
            yield return new SessionBadge("📁", l["Nearly complete folder"].Value);
        // No ⬆ glyph any more (D47): the readout beside it carried the same fact as a
        // number, and two marks for one fact is one too many. The flag stays in the model
        // — the Discord card still draws it — and a pre-baseline row simply shows nothing.
        if (CompetitiveReadout(score) is { } readout)
            yield return new SessionBadge(readout,
                l["What this score rates, against your level when the session reached it"].Value,
                "sbd-comp");
        if (score.Flags.HasFlag(HighlightFlags.FolderDebut))
            yield return new SessionBadge(detail?.FolderDebutOrdinal is { } o ? $"🆕 #{o}" : "🆕",
                l["One of your first passes in this folder"].Value);
        if (score.Flags.HasFlag(HighlightFlags.OfficialBoardPlacement) && detail?.OfficialPlace is { } place)
            yield return new SessionBadge($"🌐 ~#{place}", l["Estimated place on the official board"].Value);
        // Only ever counted on the clear that ended them, which reads as a win.
        if (detail?.AttemptsBeforeClear is { } attempts && attempts > 0)
            yield return new SessionBadge($"🎯 {attempts + 1}", l["Attempts before this clear"].Value);
        if (score.Row.IsReclear)
            yield return new SessionBadge("🔁", l["Passed in other mixes"].Value);
        // Gold, not green: the tail already carries a green delta over your previous Phoenix
        // 2 score, and a second green number measuring a different baseline eight pixels away
        // reads as the same fact twice. The words keep them apart, so "over P1" never
        // abbreviates away.
        if (includePhoenix1 && score.Phoenix1Gain is { } phoenix1)
            yield return new SessionBadge(l["+{0} over P1", phoenix1.ToString("N0")].Value,
                l["The first Phoenix 2 score to beat your Phoenix 1 best"].Value, "sbd-p1");
    }

    /// <summary>
    ///     Where you stand in the cohort the colour came from, as a place rather than a share —
    ///     "#6 of 94" is the same fact as a percentile and reads like a leaderboard, which is
    ///     what it is. Null when nothing measured this score: the surface then says nothing
    ///     about it, because a disclaimer there confuses more than it clarifies (owner call).
    /// </summary>
    internal static string? Standing(SessionScore score, IStringLocalizer<App> l)
    {
        // A broken run's standing would be the standing of a score it never achieved.
        if (score.Row.IsBroken) return null;
        return PeerStandingText.Standing(score.Standing, score.IsPerfectGame, l);
    }

    /// <summary>
    ///     "23.6 (+0.4)" — what this score rates on the competitive scale, and how far that sat
    ///     above the level the batch opened at. Both terms are needed and only one is stored:
    ///     the score's own rating is a pure function of chart level and score, while the baseline
    ///     is per-batch and had to be captured.
    /// </summary>
    private static string? CompetitiveReadout(SessionScore score)
    {
        if (score.Detail?.CompetitiveBaseline is not { } baseline) return null;
        if (score.Chart is not { } chart || score.Row.Score is not { } value || score.Row.IsBroken) return null;
        // Co-op never has a competitive comparison — the levels are Singles and Doubles only.
        if (chart.Type == ChartType.CoOp) return null;

        var rated = ScoringConfiguration.CalculateFungScore(chart.Level, PhoenixScore.From(value), chart.Type);
        var over = rated - baseline;
        return over < MinimumCompetitiveGain
            ? null
            : $"{rated:N1} (+{over:N1})";
    }
}
