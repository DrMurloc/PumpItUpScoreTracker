using MassTransit;
using ScoreTracker.Domain.Events;
using ScoreTracker.PlayerProgress.Domain;

namespace ScoreTracker.PlayerProgress.Application;

/// <summary>
///     Removes the progression-side stores that hang off scores a player just deleted: rating
///     history, session highlights, and milestones.
///     None of the three are recomputed, which is the whole reason they need telling — deleting
///     the scores behind a "you reached Expert" milestone leaves the milestone standing for a
///     title the player no longer holds.
/// </summary>
internal sealed class PlayerScoreDataDeletedConsumer : IConsumer<PlayerScoreDataDeletedEvent>
{
    private readonly IPlayerScoreDataRepository _data;

    public PlayerScoreDataDeletedConsumer(IPlayerScoreDataRepository data)
    {
        _data = data;
    }

    public async Task Consume(ConsumeContext<PlayerScoreDataDeletedEvent> context)
    {
        var message = context.Message;
        if (message.RatingHistory)
            await _data.DeleteHistory(message.UserId, message.Mix, context.CancellationToken);
        if (message.Highlights)
            await _data.DeleteHighlights(message.UserId, message.Mix, context.CancellationToken);
        if (message.Milestones)
            await _data.DeleteMilestones(message.UserId, message.Mix, context.CancellationToken);
    }
}
