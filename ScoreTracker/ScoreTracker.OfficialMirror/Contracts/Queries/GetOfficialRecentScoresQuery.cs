using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     Pulls a player's recent official-site scores with their credentials (Mirror ACL).
///     nonMapped lists site entries that couldn't be matched to known charts.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialRecentScoresQuery(string Username, RedactedString Password,
        MixEnum Mix = MixEnum.Phoenix)
    : IQuery<(IEnumerable<OfficialRecordedScore> results, IEnumerable<string> nonMapped)>
{
}
