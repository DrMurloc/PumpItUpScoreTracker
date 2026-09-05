using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     Where a player's scores stand among the peers they chose (docs/design/peers-abstraction.md
///     §4). A Domain port rather than a contract query because PlayerProgress consumes it — the
///     Hot Streak bar reads the same standing the colors do (D4) — and PlayerProgress sits
///     upstream of the vertical that can resolve every source. Rivals implements it until a Peers
///     vertical exists, the same arrangement as <see cref="IPlayerVisibilityReader" />.
/// </summary>
public interface IPeerStandingReader
{
    /// <summary>
    ///     One standing per chart the subject holds a passing score on; charts they have not passed
    ///     are absent. A null <paramref name="selection" /> reads the subject's own saved choice; a
    ///     caller that must not use it — a viewer who is not the subject (D19) — passes
    ///     <see cref="PeerSourceSelection.Default" />.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, PeerStanding>> GetStandings(Guid userId, MixEnum mix,
        IReadOnlyCollection<Guid> chartIds, PeerSourceSelection? selection = null,
        CancellationToken cancellationToken = default);
}
