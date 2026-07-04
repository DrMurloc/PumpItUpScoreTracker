using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     Pulls a player's official-site account data with their credentials (Mirror ACL —
///     credentials never leave the handler pipeline).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialAccountDataQuery(string Username, RedactedString Password,
        MixEnum Mix = MixEnum.Phoenix)
    : IQuery<PiuGameAccountDataImport>
{
}
