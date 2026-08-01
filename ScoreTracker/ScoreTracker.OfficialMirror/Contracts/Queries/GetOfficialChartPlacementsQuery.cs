using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     Estimated official placements for a batch of (chart, score) pairs. Batched because a
///     session touches dozens of charts and each board is its own row range — one query per
///     chart would put a burst on the capture path for a caption.
///     <para>
///         <paramref name="UserId" /> lets the count skip the player's own standing row where an
///         import has linked them to a board player; without the link the estimate is off by one
///         for a player already on the board, which is the honest limit of the technique.
///     </para>
///     Charts with no mirrored board, and scores that fall outside its depth, are simply absent.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialChartPlacementsQuery(
    MixEnum Mix,
    Guid UserId,
    IReadOnlyList<GetOfficialChartPlacementsQuery.ChartScore> Scores)
    : IQuery<IReadOnlyDictionary<Guid, OfficialPlacementEstimate>>
{
    [ExcludeFromCodeCoverage]
    public sealed record ChartScore(Guid ChartId, int Score);
}
