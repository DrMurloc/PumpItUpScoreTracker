using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries;

/// <summary>Pulls one UCS chart's metadata from the official site (Mirror ACL).</summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialUcsEntryQuery(int PiuGameId) : IQuery<PiuGameUcsEntry?>
{
}
