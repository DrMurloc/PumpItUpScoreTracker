using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries;

/// <summary>
///     Pulls a player's official-site account data with their credentials (Mirror ACL —
///     credentials never leave the handler pipeline).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialAccountDataQuery(string Username, string Password)
    : IQuery<PiuGameAccountDataImport>
{
}
