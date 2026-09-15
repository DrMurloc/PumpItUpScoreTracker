using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     A Phoenix 2 PUMBILITY ladder priced against a batch's Hardmode pool, derived from the
///     Hardmode milestones the same way <see cref="PumbilityLevelChange" /> derives a level
///     crossing — no new milestone kind and no new storage
///     (docs/design/hardmode-leaderboard.md D22).
///     <para>
///         Lives in Contracts rather than in Web because the ladder is the vertical's own fact:
///         <c>HardmodeSaga.Rails</c> asks the same rungs of the same totals for the Hardmode
///         page, and two implementations of one ladder drift.
///     </para>
///     <para>
///         ⚠ These bars must never render under the session page's "Titles you're working on"
///         heading. That heading is a claim about titles the player will actually earn; a
///         Hardmode rail is the same ladder over a strict subset, so a player past RED BERYL
///         reads "33% to BRONZE" with nothing on the bar to say why. The separate heading and
///         the pointer beside it are what make the bar true.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record HardmodeTitleBar(
    PumbilityPool Pool,
    string Title,
    double OldPercent,
    double NewPercent,
    double Current,
    double Required);

public static class HardmodeTitleBars
{
    /// <summary>
    ///     One bar per Hardmode pool the batch moved, in ladder order. Empty on any mix without a
    ///     gem ladder, and empty for a session that touched no qualifying chart — which is most
    ///     sessions, and is why the section can be absent rather than empty.
    /// </summary>
    public static IReadOnlyList<HardmodeTitleBar> From(MixEnum mix,
        IEnumerable<PlayerMilestoneRecord> milestones)
    {
        if (mix != MixEnum.Phoenix2) return Array.Empty<HardmodeTitleBar>();

        var records = milestones as IReadOnlyCollection<PlayerMilestoneRecord> ?? milestones.ToArray();
        var bars = new List<HardmodeTitleBar>();
        Add(PumbilityPool.Total, MilestoneKind.HardmodePumbilityGain);
        Add(PumbilityPool.Singles, MilestoneKind.HardmodeSinglesPumbilityGain);
        Add(PumbilityPool.Doubles, MilestoneKind.HardmodeDoublesPumbilityGain);
        return bars;

        void Add(PumbilityPool pool, MilestoneKind kind)
        {
            // A pool nudged by several batches spans the session: earliest old, latest new —
            // the same rule the real title bars follow.
            var moved = records
                .Where(m => m.Kind == kind && m is { OldValue: not null, NewValue: not null })
                .OrderBy(m => m.OccurredAt)
                .ToArray();
            if (moved.Length == 0) return;

            var oldValue = moved.First().OldValue!.Value;
            var newValue = moved.Last().NewValue!.Value;

            var rungs = Phoenix2TitleList.BuildList().OfType<Phoenix2PumbilityTitle>()
                .Where(t => t.Pool == pool)
                .OrderBy(t => t.CompletionRequired)
                .ToArray();
            if (rungs.Length == 0) return;

            // The rung being worked on is the one ahead of where the pool ENDED. A pool that
            // cleared its top rung has none ahead of it, and then the bar is simply full.
            var next = rungs.FirstOrDefault(t => t.CompletionRequired > newValue);
            var floor = rungs.LastOrDefault(t => t.CompletionRequired <= newValue)?.CompletionRequired ?? 0;
            var required = next?.CompletionRequired ?? rungs[^1].CompletionRequired;
            var title = next?.Name.ToString() ?? rungs[^1].Name.ToString();

            // Floor-aware, so the bar spans the rung being worked on rather than the whole
            // ladder — otherwise every early Hardmode pool renders as a stub near zero.
            var span = required - floor;
            bars.Add(new HardmodeTitleBar(pool, title,
                Percent(oldValue, floor, span), Percent(newValue, floor, span), newValue, required));
        }
    }

    private static double Percent(double value, double floor, double span)
    {
        return span <= 0 ? 1 : Math.Clamp((value - floor) / span, 0, 1);
    }
}
