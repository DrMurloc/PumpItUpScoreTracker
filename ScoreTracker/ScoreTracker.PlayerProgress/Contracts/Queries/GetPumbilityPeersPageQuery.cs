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
    ///     no other mix has PUMBILITY peers. <paramref name="Energy" /> is the rung each entry's
    ///     <see cref="PeerPoolEntry.Projected" /> is read at (D51, D52).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetPumbilityPeersPageQuery(Guid UserId, MixEnum Mix = MixEnum.Phoenix,
        ChartType? Pool = null, Energy Energy = Energy.Good) : IQuery<PumbilityPeersPageRecord>;
}
