using MediatR;

namespace ScoreTracker.Randomizer.Contracts.Events
{
    /// <summary>
    ///     In-process fact (MediatR notification, never the bus): a draw's cards or states
    ///     changed. Subscribed circuits — staff devices and spectator pages — re-query the
    ///     draw and re-render. State is already persisted when this fires.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record DrawUpdatedEvent(Guid DrawId, Guid Slug) : INotification
    {
    }
}
