using ScoreTracker.Domain.Models;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     Who may look at whom — a published read port (ADR-001), the same shape as
///     <see cref="IScoreReader" /> and <see cref="IPlayerStatsReader" />. The bases are consent
///     grants: yourself, a public profile, a user-created community you share, a rival edge you
///     hold. This is <em>visibility</em>, not <em>peers</em> — who you may see, not who you are
///     measured against (docs/design/peers-abstraction.md §1). Rivals implements it for now; a
///     consumer depends on this port and never on the vertical behind it.
/// </summary>
public interface IPlayerVisibilityReader
{
    /// <summary>
    ///     Everything the viewer may see beyond the public players, read once. A <c>null</c>
    ///     viewer is anonymous and gets <see cref="PlayerAudience.Anonymous" />.
    /// </summary>
    Task<PlayerAudience> GetAudience(Guid? viewerId, CancellationToken cancellationToken = default);
}
