using MediatR;
using ScoreTracker.Application.Dtos;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

public sealed record SearchForUsersQuery(string SearchText, int Page, int Count) : IRequest<SearchResultDto<User>>
{
}