using MediatR;
using ScoreTracker.Domain.Events;
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
