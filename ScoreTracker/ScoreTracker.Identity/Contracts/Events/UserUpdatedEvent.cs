using ScoreTracker.Domain.Models;

namespace ScoreTracker.Identity.Contracts.Events
{
    [ExcludeFromCodeCoverage]
    public sealed record UserUpdatedEvent(Guid UserId, string? Country, bool IsPublic)
    {
        /// <summary>
        ///     Builds the event from the user record that was just persisted. Publishers use this
        ///     instead of the constructor so the flags can only come from a saved <see cref="User" />:
        ///     the signed-in user carried by ICurrentUserAccessor is a claims snapshot that refreshes
        ///     only after the update completes, so reading the flags off it announces the state from
        ///     before the save — which subscribers act on as if it were the new state.
        /// </summary>
        public static UserUpdatedEvent From(User user)
        {
            return new UserUpdatedEvent(user.Id, user.Country, user.IsPublic);
        }
    }
}
