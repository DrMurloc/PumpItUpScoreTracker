using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.Services;

/// <summary>
///     What happens when a score arrives for a weekly chart a player already has an entry on
///     (weekly-charts-overhaul.md §9.3). Two callers share the board — the official import,
///     which re-registers your best on every run and must never move it, and the Record
///     dialog, where a player can correct what they typed. <see cref="WeeklyEntryIntent" />
///     picks the rule; this decides the outcome.
///     Pure, like <see cref="WeeklyChartSuggestionPolicy" /> beside it: the boards' write rules
///     and their ranking rules are both domain policy, and both must be one definition.
/// </summary>
public static class WeeklyEntryMergePolicy
{
    /// <param name="existing">The stored entry and its trust source, or null for a first recording.</param>
    /// <param name="incoming">The submitted entry.</param>
    /// <param name="incomingSource">The trust tier the submission is claimed under.</param>
    /// <param name="intent">Which rule the caller is asking for.</param>
    /// <param name="competitiveLevel">
    ///     The player's competitive level for this chart's type, recomputed now. Always stamped
    ///     onto the persisted entry — the band verdict has to describe the player today, not
    ///     whenever the row was first written.
    /// </param>
    public static WeeklyEntryMerge Merge(
        (WeeklyTournamentEntry Entry, ChallengeEntrySource Source)? existing,
        WeeklyTournamentEntry incoming,
        ChallengeEntrySource incomingSource,
        WeeklyEntryIntent intent,
        double competitiveLevel)
    {
        if (existing == null)
            return new WeeklyEntryMerge(incoming with { CompetitiveLevel = competitiveLevel },
                incomingSource, IsImprovement: true, IsRefused: false);

        var (current, currentSource) = existing.Value;

        if (intent == WeeklyEntryIntent.Replace)
        {
            // A hand amend can only touch a self-report. An imported score would be raised
            // straight back by the next import, so allowing it would persist a value that
            // silently reverts.
            if (currentSource != ChallengeEntrySource.Manual)
                return new WeeklyEntryMerge(current, currentSource, IsImprovement: false, IsRefused: true);

            // The submission wins wholesale, except the photo: proof already attached is not
            // the player's to lose by leaving the upload empty (M3).
            return new WeeklyEntryMerge(
                incoming with
                {
                    CompetitiveLevel = competitiveLevel,
                    PhotoUrl = incoming.PhotoUrl ?? current.PhotoUrl
                },
                incomingSource,
                IsImprovement: incoming.Score > current.Score,
                IsRefused: false);
        }

        var merged = current;
        // The source describes the RANKED score's provenance, so it moves only when the score
        // does — a weaker manual submit never demotes a verified one.
        var source = currentSource;
        var improved = incoming.Score > current.Score;
        if (improved)
        {
            merged = merged with { Score = incoming.Score };
            source = incomingSource;
        }

        if (incoming.Plate > merged.Plate) merged = merged with { Plate = incoming.Plate };
        if (!incoming.IsBroken && merged.IsBroken) merged = merged with { IsBroken = false };

        merged = merged with
        {
            CompetitiveLevel = competitiveLevel,
            PhotoUrl = incoming.PhotoUrl ?? merged.PhotoUrl
        };
        return new WeeklyEntryMerge(merged, source, improved, IsRefused: false);
    }
}
