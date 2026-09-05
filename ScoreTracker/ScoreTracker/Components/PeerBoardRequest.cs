using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Web.Components;

/// <summary>
///     A popover source line asking its host to open that source's own board in the chart details
///     dialog (docs/design/peers-abstraction.md D12) — no "your peers" scope exists, each line lands
///     on the board the site already has for that group. <paramref name="Subject" /> is whose peers
///     the line counted: a host that shows another player's scores stamps their id so the
///     Competitive board is their band (D31); null is the viewer.
/// </summary>
public sealed record PeerBoardRequest(Guid ChartId, ChartLeaderboardScopes.LeaderboardScope Scope, Name? Community,
    Guid? Subject = null)
{
    public static PeerBoardRequest For(Guid chartId, PeerStandingSource source) => source.Kind switch
    {
        PeerSourceKind.Rivals => new PeerBoardRequest(chartId, ChartLeaderboardScopes.LeaderboardScope.Rivals, null),
        PeerSourceKind.CompetitiveLevel => new PeerBoardRequest(chartId,
            ChartLeaderboardScopes.LeaderboardScope.CompetitivePeers, null),
        PeerSourceKind.Pumbility => new PeerBoardRequest(chartId,
            ChartLeaderboardScopes.LeaderboardScope.PumbilityPeers, null),
        _ when source.IsWorld => new PeerBoardRequest(chartId, ChartLeaderboardScopes.LeaderboardScope.World, null),
        _ when source.IsRegional => new PeerBoardRequest(chartId, ChartLeaderboardScopes.LeaderboardScope.Region,
            null),
        _ => new PeerBoardRequest(chartId, ChartLeaderboardScopes.LeaderboardScope.Community,
            source.CommunityName == null ? null : Name.From(source.CommunityName))
    };
}
