using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries;

/// <summary>PNG bytes for a composed tier-list share card (Download / og:image).</summary>
[ExcludeFromCodeCoverage]
public sealed record GetTierListShareCardQuery(TierListShareCard Card) : IQuery<byte[]>
{
}
