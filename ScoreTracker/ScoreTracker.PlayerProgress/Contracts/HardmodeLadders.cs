using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>A Phoenix 2 ladder rung a Hardmode pool crossed — the same rung names the real ladders use.</summary>
[ExcludeFromCodeCoverage]
public sealed record HardmodeRung(PumbilityPool Pool, string Title, int Threshold);

/// <summary>
///     The ladder rungs and the level crossing a batch's Hardmode pools moved through, derived from
///     the Hardmode milestones the capture step already writes — no new milestone kind and no storage
///     (docs/design/hardmode-leaderboard.md D33). The Discord card lists them and the feed's rung row
///     reads the same answer, so the two cannot disagree about which rung was crossed.
/// </summary>
public static class HardmodeLadders
{
    private static readonly (PumbilityPool Pool, MilestoneKind Kind)[] Pools =
    {
        (PumbilityPool.Total, MilestoneKind.HardmodePumbilityGain),
        (PumbilityPool.Singles, MilestoneKind.HardmodeSinglesPumbilityGain),
        (PumbilityPool.Doubles, MilestoneKind.HardmodeDoublesPumbilityGain)
    };

    /// <summary>
    ///     Every rung each pool crossed, in ladder order (combined, singles, doubles) and lowest rung
    ///     first — listed the way title completions are. A pool moved by several batches spans them:
    ///     earliest old, latest new, the rule <see cref="HardmodeTitleBars" /> follows. Empty on any mix
    ///     without the gem ladders.
    /// </summary>
    public static IReadOnlyList<HardmodeRung> RungsCrossed(MixEnum mix,
        IEnumerable<PlayerMilestoneRecord> milestones)
    {
        if (mix != MixEnum.Phoenix2) return Array.Empty<HardmodeRung>();

        var records = milestones as IReadOnlyCollection<PlayerMilestoneRecord> ?? milestones.ToArray();
        var crossed = new List<HardmodeRung>();
        foreach (var (pool, kind) in Pools)
        {
            if (Span(records, kind) is not { } move) continue;
            crossed.AddRange(Phoenix2TitleList.BuildList().OfType<Phoenix2PumbilityTitle>()
                .Where(t => t.Pool == pool && t.CompletionRequired > move.Old && t.CompletionRequired <= move.New)
                .OrderBy(t => t.CompletionRequired)
                .Select(t => new HardmodeRung(pool, t.Name.ToString(), t.CompletionRequired)));
        }

        return crossed;
    }

    /// <summary>
    ///     The combined Hardmode pool's level crossing ("BRONZE LV.1 → LV.3"), or null when there is
    ///     nothing to say: no movement, a movement inside one level, or a batch that crossed into a new
    ///     gem — that rung's own line already says it, the rule <see cref="PumbilityLevelChange" /> keeps
    ///     for PUMBILITY. Only the combined pool has levels; the singles and doubles ladders do not.
    /// </summary>
    public static PumbilityLevelChange? LevelChange(MixEnum mix, IEnumerable<PlayerMilestoneRecord> milestones)
    {
        if (mix != MixEnum.Phoenix2) return null;

        var records = milestones as IReadOnlyCollection<PlayerMilestoneRecord> ?? milestones.ToArray();
        if (Span(records, MilestoneKind.HardmodePumbilityGain) is not { } move) return null;

        var from = Phoenix2PumbilityLevel.From(move.Old);
        var to = Phoenix2PumbilityLevel.From(move.New);
        if (to.Index <= from.Index) return null;

        return RungsCrossed(mix, records).Any(r => r.Pool == PumbilityPool.Total)
            ? null
            : new PumbilityLevelChange(from, to, move.Old, move.New);
    }

    private static (double Old, double New)? Span(IEnumerable<PlayerMilestoneRecord> records, MilestoneKind kind)
    {
        var moved = records
            .Where(m => m.Kind == kind && m is { OldValue: not null, NewValue: not null })
            .OrderBy(m => m.OccurredAt)
            .ToArray();
        return moved.Length == 0 ? null : (moved[0].OldValue!.Value, moved[^1].NewValue!.Value);
    }
}
