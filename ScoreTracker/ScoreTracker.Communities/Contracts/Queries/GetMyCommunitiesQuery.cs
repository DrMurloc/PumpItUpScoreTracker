using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Communities.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetMyCommunitiesQuery : IQuery<IEnumerable<CommunityOverviewRecord>>
{
}
