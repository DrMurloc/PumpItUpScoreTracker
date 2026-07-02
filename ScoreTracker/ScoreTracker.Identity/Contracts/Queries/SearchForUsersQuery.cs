using MediatR;
using ScoreTracker.Application.Dtos;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Identity.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record SearchForUsersQuery(string SearchText, int Page, int Count) : IQuery<SearchResultDto<User>>
{
}
