using ScoreTracker.PlayerProgress.Contracts.Events;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     What a player with Hardmode off does not see announced (docs/design/hardmode-leaderboard.md
///     D30): the skull flag, the Hardmode rank and gain a score carries, and the three Hardmode pool
///     milestones — and nothing else. The session page and the Discord card both strip through this,
///     so the two cannot disagree about what "off" removes.
///     <para>
///         It strips a copy. The capture step still writes every one of these facts for every
///         player; that is what lets the session page follow the switch backwards.
///     </para>
/// </summary>
public static class HardmodeVisibility
{
    public static bool IsHardmode(MilestoneKind kind)
    {
        return kind is MilestoneKind.HardmodePumbilityGain or MilestoneKind.HardmodeSinglesPumbilityGain
            or MilestoneKind.HardmodeDoublesPumbilityGain;
    }

    /// <summary>
    ///     Whether a batch carries any Hardmode fact at all. A consumer asks this before reading the
    ///     player's switch, so a batch with nothing to strip never costs a settings read.
    /// </summary>
    public static bool Carries(ScoreHighlightsCapturedEvent e)
    {
        return e.Milestones.Any(m => IsHardmode(m.Kind)) ||
               e.Changes.Any(c => c.Flags.HasFlag(HighlightFlags.HardmodeTop50));
    }

    public static HighlightFlags Strip(HighlightFlags flags)
    {
        return flags & ~HighlightFlags.HardmodeTop50;
    }

    public static HighlightDetail? Strip(HighlightDetail? detail)
    {
        return detail == null ? null : detail with { HardmodeGain = null, HardmodeRank = null };
    }

    public static IReadOnlyList<PlayerMilestoneRecord> Strip(IEnumerable<PlayerMilestoneRecord> milestones)
    {
        return milestones.Where(m => !IsHardmode(m.Kind)).ToArray();
    }

    public static ScoreHighlightRecord Strip(ScoreHighlightRecord highlight)
    {
        return highlight with { Flags = Strip(highlight.Flags), Detail = Strip(highlight.Detail) };
    }

    /// <summary>
    ///     The whole batch, for a consumer that renders from the event. A score whose only flag was
    ///     the skull comes back unflagged, which matters wherever "any flag" decides prominence — the
    ///     Discord card promotes every flagged score to an art row.
    /// </summary>
    public static ScoreHighlightsCapturedEvent Strip(ScoreHighlightsCapturedEvent e)
    {
        return e with
        {
            Changes = e.Changes.Select(c => c with { Flags = Strip(c.Flags), Detail = Strip(c.Detail) }).ToArray(),
            Milestones = Strip(e.Milestones)
        };
    }
}
