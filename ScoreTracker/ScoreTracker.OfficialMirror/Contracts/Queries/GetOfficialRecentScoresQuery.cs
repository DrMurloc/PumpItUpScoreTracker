using ScoreTracker.Domain.Records;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     Pulls a player's recent official-site scores with their credentials (Mirror ACL).
///     nonMapped lists site entries that couldn't be matched to known charts.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialRecentScoresQuery(string Username, string Password)
    : IQuery<(IEnumerable<OfficialRecordedScore> results, IEnumerable<string> nonMapped)>
{
}
