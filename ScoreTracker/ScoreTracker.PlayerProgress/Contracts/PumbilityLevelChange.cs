using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     A PUMBILITY level crossing derived from a batch's milestones — the one rule every surface
///     shares (the Discord session card, the session page's strip, the highlight feeds), written
///     here so it cannot drift between them (docs/design/pumbility-levels.md §5).
///     <para>
///         Derives from the existing <see cref="MilestoneKind.PumbilityGain" /> milestone's
///         OldValue → NewValue — no new milestone kind, no new storage. Returns null when the same
///         batch also completed a [P.B] gem title: crossing into RED BERYL LV.1 IS that title, and
///         saying both is saying it twice. This is "didn't change titles but changed levels",
///         stated from the other side.
///     </para>
/// </summary>
public sealed record PumbilityLevelChange(
    Phoenix2PumbilityLevel From,
    Phoenix2PumbilityLevel To,
    double OldPool,
    double NewPool)
{
    // The total-pool gems, resolved from the shipped taxonomy so a renamed gem cannot leave a
    // stale name here. The [S]/[D] ladders are deliberately absent: their titles have no levels,
    // so completing one says nothing about the gem ladder and suppresses nothing.
    private static readonly IReadOnlySet<string> TotalPumbilityGems = Phoenix2TitleList.BuildList()
        .OfType<Phoenix2PumbilityTitle>()
        .Where(t => t.Pool == PumbilityPool.Total)
        .Select(t => t.Name.ToString())
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     The batch's level crossing, or null when there is nothing to say: a mix with no gem
    ///     ladder, no PUMBILITY movement, a movement inside one rung, or a crossing whose gem
    ///     title completed in the same batch and is already the headline.
    /// </summary>
    public static PumbilityLevelChange? TryFrom(MixEnum mix, IEnumerable<PlayerMilestoneRecord> milestones)
    {
        if (mix != MixEnum.Phoenix2) return null;

        var records = milestones as IReadOnlyCollection<PlayerMilestoneRecord> ?? milestones.ToArray();
        var gain = records.FirstOrDefault(m =>
            m is { Kind: MilestoneKind.PumbilityGain, OldValue: not null, NewValue: not null });
        if (gain == null) return null;

        var from = Phoenix2PumbilityLevel.From(gain.OldValue!.Value);
        var to = Phoenix2PumbilityLevel.From(gain.NewValue!.Value);
        if (to.Index <= from.Index) return null;

        var completedAGem = records.Any(m =>
            m.Kind == MilestoneKind.TitleCompleted && m.Title is { } title &&
            TotalPumbilityGems.Contains(title));
        return completedAGem
            ? null
            : new PumbilityLevelChange(from, to, gain.OldValue.Value, gain.NewValue.Value);
    }

    /// <summary>
    ///     "DIAMOND LV.4" — the display form every surface shares. Gem names are the game's own
    ///     proper nouns and never localize; the [P.B] prefix drops because these strings render
    ///     where the ladder is already the subject. Empty for the unranked rung, whose only name
    ///     is a localized word the caller owns.
    /// </summary>
    public static string LabelFor(Phoenix2PumbilityLevel level)
    {
        if (level.Gem is not { } gem) return string.Empty;
        var name = gem.ToString().Replace("[P.B] ", "");
        return level.Level is { } lv ? $"{name} LV.{lv}" : name;
    }

    /// <summary>
    ///     "DIAMOND LV.3 → LV.4" — the gem stated once when the crossing stays inside it, in
    ///     full when it changes, and the destination alone when the player was unranked before
    ///     (an arrow out of a rung with no name says less than the rung reached).
    /// </summary>
    public string CrossingText()
    {
        if (!From.IsRanked) return LabelFor(To);
        var to = From.Gem is { } fromGem && To.Gem is { } toGem && fromGem == toGem && To.Level is { } level
            ? $"LV.{level}"
            : LabelFor(To);
        return $"{LabelFor(From)} → {to}";
    }
}
