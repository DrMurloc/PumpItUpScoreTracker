using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Events;

/// <summary>
///     A player deleted score data and the Ledger has removed its own. Progression-side stores
///     keyed to the same scores — rating history, highlights, milestones — are PlayerProgress's
///     to remove, and it consumes this to do so.
///     They ride an event rather than a port because none of them are recomputed: deleting the
///     scores behind a milestone does not clear the milestone, it strands it.
///     <paramref name="Mix" /> null means every mix.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PlayerScoreDataDeletedEvent(
    Guid UserId,
    MixEnum? Mix,
    bool RatingHistory,
    bool Highlights,
    bool Milestones)
{
    public bool AnythingToDo => RatingHistory || Highlights || Milestones;
}
