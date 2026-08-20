using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    /// <summary>
    ///     The Phoenix 2 Play page's read: what the viewer's PUMBILITY peers build their number
    ///     from, and who they are (docs/design/pumbility-overhaul.md §3.10). Reads the same cached
    ///     sweep the projection does, so a cold visit pays for one sweep, not two.
    ///     <paramref name="Pool" /> null is both types merged into one list (D37); naming a type
    ///     scopes the peers, the list and the roster to it. Any mix but Phoenix 2 answers empty —
    ///     no other mix has PUMBILITY peers.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetPumbilityPeersPageQuery(Guid UserId, MixEnum Mix = MixEnum.Phoenix,
        ChartType? Pool = null) : IQuery<PumbilityPeersPageRecord>;
}
