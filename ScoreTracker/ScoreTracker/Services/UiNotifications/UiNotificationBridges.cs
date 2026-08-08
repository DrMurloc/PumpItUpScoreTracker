using ScoreTracker.OfficialMirror.Contracts.Events;
using MassTransit;
using MediatR;
using ScoreTracker.Domain.Events;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.Randomizer.Contracts.Events;

namespace ScoreTracker.Web.Services.UiNotifications;

// These MediatR notification handlers are the one bridge from in-process domain events to the UI
// hub. They are plain DI services (not a Blazor component pretending to be a handler), so MediatR
// resolves them normally and there is no static event in the middle.

internal sealed class ImportStatusUiBridge :
    INotificationHandler<ImportStatusUpdatedEvent>,
    INotificationHandler<ImportStatusErrorEvent>
{
    private readonly IUiNotificationHub _hub;

    public ImportStatusUiBridge(IUiNotificationHub hub)
    {
        _hub = hub;
    }

    public Task Handle(ImportStatusUpdatedEvent notification, CancellationToken cancellationToken)
    {
        return _hub.PublishAsync(UiTopics.User(notification.UserId), notification);
    }

    public Task Handle(ImportStatusErrorEvent notification, CancellationToken cancellationToken)
    {
        return _hub.PublishAsync(UiTopics.User(notification.UserId), notification);
    }
}

/// <summary>
///     Carries a finished Score check to whichever circuit is still watching. Nothing stores the
///     verdict, so this hop IS the delivery — a player who navigated away simply never receives it.
/// </summary>
internal sealed class ImportCheckUiBridge : INotificationHandler<ImportCheckCompletedEvent>
{
    private readonly IUiNotificationHub _hub;

    public ImportCheckUiBridge(IUiNotificationHub hub)
    {
        _hub = hub;
    }

    public Task Handle(ImportCheckCompletedEvent notification, CancellationToken cancellationToken)
    {
        return _hub.PublishAsync(UiTopics.User(notification.UserId), notification);
    }
}

internal sealed class PlayerStatsUiBridge : INotificationHandler<PlayerStatsUpdatedEvent>
{
    private readonly IUiNotificationHub _hub;

    public PlayerStatsUiBridge(IUiNotificationHub hub)
    {
        _hub = hub;
    }

    public Task Handle(PlayerStatsUpdatedEvent notification, CancellationToken cancellationToken)
    {
        return _hub.PublishAsync(UiTopics.User(notification.UserId), notification);
    }
}

/// <summary>
///     Carries a finished capture to whichever circuit is watching that player's session.
///     <para>
///         A bus consumer rather than a MediatR handler, because capture announces itself on the
///         bus — and PUBLIC, because the host's own assembly scan only picks up public consumers
///         (vertical consumers are internal and register through their own hooks).
///     </para>
///     <para>
///         This is the event a page waiting on capture should listen for, and the reason it need
///         not poll. Scores are held as a batch for two minutes past the LAST of them before
///         capture even begins, so any timer aimed at that is guessing at someone else's
///         schedule — and every guess this page made was wrong in a different way.
///     </para>
/// </summary>
public sealed class ScoreHighlightsCapturedUiBridge : IConsumer<ScoreHighlightsCapturedEvent>
{
    private readonly IUiNotificationHub _hub;

    public ScoreHighlightsCapturedUiBridge(IUiNotificationHub hub)
    {
        _hub = hub;
    }

    public Task Consume(ConsumeContext<ScoreHighlightsCapturedEvent> context)
    {
        return _hub.PublishAsync(UiTopics.User(context.Message.UserId), context.Message);
    }
}

internal sealed class DrawUpdatedUiBridge : INotificationHandler<DrawUpdatedEvent>
{
    private readonly IUiNotificationHub _hub;

    public DrawUpdatedUiBridge(IUiNotificationHub hub)
    {
        _hub = hub;
    }

    public Task Handle(DrawUpdatedEvent notification, CancellationToken cancellationToken)
    {
        return _hub.PublishAsync(UiTopics.Draws, notification);
    }
}
