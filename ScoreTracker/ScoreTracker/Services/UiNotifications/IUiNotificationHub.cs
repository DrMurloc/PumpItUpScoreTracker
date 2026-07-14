namespace ScoreTracker.Web.Services.UiNotifications;

// Routes a background/domain notification to just the Blazor circuits that asked for it, keyed by
// an arbitrary topic string — a user id for per-user updates (import status, rating), a shared
// constant for broadcast resources (randomizer draws). Replaces the old static-event bridge on
// MainLayout, which fanned every event out to every circuit and leaned on each handler to filter
// by user id (the source of the cross-user leaks).
public interface IUiNotificationHub
{
    // Subscribe a circuit's handler to (topic, T); dispose to unsubscribe (do it in the component's
    // Dispose). The handler is invoked off a background thread — marshal to the circuit with
    // InvokeAsync inside it.
    IDisposable Subscribe<T>(string topic, Func<T, Task> handler);

    // Deliver a message to every handler subscribed to (topic, T). One failing handler (e.g. a
    // circuit torn down mid-flight) never stops the others.
    Task PublishAsync<T>(string topic, T message);
}

// A device-local import credential was stored or forgotten (configurator / upload page). The
// import widget listens so its saved-vs-typed state flips without a page refresh. UI-only — this
// never travels the domain bus (no password, no persistence; just a nudge to re-read local storage).
public sealed record ImportCredentialChanged(Guid UserId);

// Topic keys. Per-user topics keep one user's updates off every other user's circuits.
public static class UiTopics
{
    public static string User(Guid userId) => $"user:{userId:N}";

    // Randomizer draws aren't user-scoped — every live viewer/staff device watches the same feed
    // and filters to its own slug and siblings.
    public const string Draws = "draws";
}
