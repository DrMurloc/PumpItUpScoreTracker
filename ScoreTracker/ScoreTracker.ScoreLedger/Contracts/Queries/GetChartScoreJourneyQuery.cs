using ScoreTracker.Domain.Records;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     A player's journaled submission history for one chart across every mix, oldest
///     first (each entry carries its Mix). Backs the tier-list details dialog's
///     "your journey" timeline. UserId defaults to the current user; reads of another
///     player honor the profile-privacy access gate and return empty when denied.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartScoreJourneyQuery(Guid ChartId, Guid? UserId = null)
    : IQuery<IEnumerable<ScoreJournalEntry>>
{
}
