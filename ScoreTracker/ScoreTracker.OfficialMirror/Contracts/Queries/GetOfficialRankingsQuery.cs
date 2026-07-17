using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     The player rankings hub: Phoenix 2 ranks by the mirrored official PUMBILITY board
///     (Type: "All"/"Singles"/"Doubles"); a mix without one (Phoenix) ranks by the computed
///     rating over its mirrored chart boards.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialRankingsQuery(MixEnum Mix, string Type = "All")
    : IQuery<OfficialRankingsRecord>;
