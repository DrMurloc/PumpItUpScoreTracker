namespace ScoreTracker.Domain.Records;

/// <summary>
///     What <see cref="Services.WeeklyEntryMergePolicy" /> decided about a weekly-board
///     submission. <paramref name="IsImprovement" /> and <paramref name="IsRefused" /> are
///     facts about the merge itself, not about the resulting entry — a caller that re-derived
///     them from <paramref name="Entry" /> would be re-implementing the rule.
/// </summary>
/// <param name="Entry">The entry to persist. Unchanged from the stored one when nothing won.</param>
/// <param name="Source">The trust tier the persisted score is claimed under.</param>
/// <param name="IsImprovement">
///     A first recording, or a ranked score that went up. The gate on the progression event
///     (weekly-charts-overhaul.md §9.5) — a correction downward is never progress.
/// </param>
/// <param name="IsRefused">
///     The submission asked for something the board does not allow, and nothing should be
///     written. Today that is only a hand amend against an officially imported entry (§9.4).
/// </param>
[ExcludeFromCodeCoverage]
public sealed record WeeklyEntryMerge(
    WeeklyTournamentEntry Entry,
    ChallengeEntrySource Source,
    bool IsImprovement,
    bool IsRefused);
