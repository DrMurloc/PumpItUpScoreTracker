using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     Chart popularity from the latest sealed snapshot with a short trend window.
///     Chart-keyed: song-level views aggregate client-side against the catalog.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialPopularityQuery(MixEnum Mix, int TrendSnapshots = 8)
    : IQuery<IReadOnlyList<OfficialPopularityRecord>>;
