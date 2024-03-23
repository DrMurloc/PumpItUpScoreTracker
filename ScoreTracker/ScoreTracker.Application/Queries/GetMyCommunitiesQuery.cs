using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries;

public sealed record GetMyCommunitiesQuery : IRequest<IEnumerable<CommunityOverviewRecord>>
{
}