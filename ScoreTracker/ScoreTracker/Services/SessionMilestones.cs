using ScoreTracker.PlayerProgress.Contracts;

namespace ScoreTracker.Web.Services;

/// <summary>
///     A session's milestones as one session moved them. Capture mints milestones per batch, and a
///     session holds many batches — a long import drains in several, and a folded night holds
///     several imports (docs/design/session-breakdown.md §8.3) — so a pool that rose three times in
///     a night arrives as three milestones. Rendered as they were stored, the hero printed a strip
///     per import and the history card a headline per import, for what the player did once.
/// </summary>
internal static class SessionMilestones
{
    /// <summary>
    ///     Kinds that report one running value moving: a pool, a competitive level, an estimated
    ///     place on a board. The session moved each from its earliest old value to its latest new
    ///     one — the rule the ceremony band and the title bars already follow.
    /// </summary>
    private static readonly HashSet<MilestoneKind> RunningValues = new()
    {
        MilestoneKind.PumbilityGain,
        MilestoneKind.SinglesPumbilityGain,
        MilestoneKind.DoublesPumbilityGain,
        MilestoneKind.SinglesCompetitiveGain,
        MilestoneKind.DoublesCompetitiveGain,
        MilestoneKind.OfficialPumbilityRank,
        MilestoneKind.HardmodePumbilityGain,
        MilestoneKind.HardmodeSinglesPumbilityGain,
        MilestoneKind.HardmodeDoublesPumbilityGain
    };

    /// <summary>
    ///     One milestone per running value (per board, for the estimated place) and one folder
    ///     movement per folder, each spanning the session. Everything else is an event that happens
    ///     once — a title completing, a folder lamping — and passes through as it was captured.
    /// </summary>
    public static IReadOnlyList<PlayerMilestoneRecord> Collapse(IEnumerable<PlayerMilestoneRecord> milestones)
    {
        var all = milestones.ToArray();
        var collapsed = new List<PlayerMilestoneRecord>();

        foreach (var run in all.Where(m => RunningValues.Contains(m.Kind)).GroupBy(m => (m.Kind, m.Detail)))
        {
            var ordered = run.OrderBy(m => m.OccurredAt).ToArray();
            // A first placing has no old figure, and a session that began unplaced still did.
            collapsed.Add(ordered[^1] with { OldValue = ordered[0].OldValue });
        }

        foreach (var folder in all.Where(m => m.Kind == MilestoneKind.FolderProgress)
                     .GroupBy(m => FolderProgressDetail.TryParse(m.Detail)?.Folder ?? m.Detail))
            collapsed.AddRange(SpanFolder(folder.OrderBy(m => m.OccurredAt).ToArray()));

        collapsed.AddRange(all.Where(m => !RunningValues.Contains(m.Kind) && m.Kind != MilestoneKind.FolderProgress));
        return collapsed;
    }

    /// <summary>
    ///     One folder's movements across the session: its latest tier and grade, from the tier and
    ///     grade it held before the first batch that moved each. A payload this version cannot read
    ///     passes through untouched rather than being guessed at.
    /// </summary>
    private static IEnumerable<PlayerMilestoneRecord> SpanFolder(IReadOnlyList<PlayerMilestoneRecord> ordered)
    {
        var details = ordered.Select(m => FolderProgressDetail.TryParse(m.Detail)).ToArray();
        if (ordered.Count == 1 || details.Any(d => d == null)) return ordered;

        var spanned = details[^1]! with
        {
            FromTier = details.FirstOrDefault(d => d!.TierMoved)?.FromTier,
            FromGrade = details.FirstOrDefault(d => d!.GradeMoved)?.FromGrade
        };
        return new[] { ordered[^1] with { Detail = spanned.Format() } };
    }
}
