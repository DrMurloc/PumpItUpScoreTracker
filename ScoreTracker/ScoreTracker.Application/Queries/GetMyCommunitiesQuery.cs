using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetMyCommunitiesQuery : IQuery<IEnumerable<CommunityOverviewRecord>>
{
}
