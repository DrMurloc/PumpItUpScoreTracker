using MediatR;
using ScoreTracker.Domain.Models.Titles;

namespace ScoreTracker.Application.Queries;

public sealed record GetXXTitleProgressQuery : IRequest<IEnumerable<TitleProgress>>
{
}