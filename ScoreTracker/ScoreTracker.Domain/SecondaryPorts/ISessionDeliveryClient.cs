using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     Hands a live piugame.com session to the community tools a player has explicitly granted it to.
///     <para>
///         A Domain port because the two sides cannot reference each other. The sid exists only
///         inside OfficialMirror, only during an import; the entitlement to receive it lives in
///         CommunityTools; and a vertical never references another vertical. This is the same
///         cycle-breaking escape hatch <c>IDiscordFeedReader</c> already established for
///         OfficialMirror → Communities.
///     </para>
///     <para>
///         Fire-and-forget by construction. Unlike a score-push delivery there is no durable queue
///         behind this and there cannot be: persisting the payload would mean writing a live
///         credential to a table. If a maker's server is down when it fires, the delivery is gone.
///     </para>
/// </summary>
public interface ISessionDeliveryClient
{
    /// <summary>
    ///     Delivers to every tool in PIUGame-session mode that this player granted access to
    ///     explicitly. Never throws into the caller — a failing tool must not fail the player's
    ///     import.
    /// </summary>
    Task DeliverSession(Guid userId, MixEnum mix, RedactedString sid, string gameTag,
        CancellationToken cancellationToken = default);
}
