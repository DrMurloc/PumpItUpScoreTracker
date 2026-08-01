using MassTransit;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.Domain.Events;

namespace ScoreTracker.PlayerProgress.Application;

/// <summary>
///     Drops the highlights and milestones a session produced when that session is undone.
///     Neither is recomputed from scores, so an undo that left them behind would keep claiming
///     the player hit a title they no longer hold.
/// </summary>
internal sealed class ScoreSessionUndoneConsumer : IConsumer<ScoreSessionUndoneEvent>
{
    private readonly IPlayerScoreDataRepository _data;

    public ScoreSessionUndoneConsumer(IPlayerScoreDataRepository data)
    {
        _data = data;
    }

    public Task Consume(ConsumeContext<ScoreSessionUndoneEvent> context)
    {
        return _data.DeleteForSession(context.Message.UserId, context.Message.SessionId,
            context.CancellationToken);
    }
}
