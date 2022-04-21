using MediatR;
using ScoreTracker.Domain.Models.Titles;

namespace ScoreTracker.Application.Queries;

public sealed record GetTitleProgressQuery : IRequest<IEnumerable<TitleProgress>>
{
}