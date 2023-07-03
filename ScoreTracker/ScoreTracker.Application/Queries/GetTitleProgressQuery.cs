using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models.Titles;

namespace ScoreTracker.Application.Queries;

public sealed record GetTitleProgressQuery(MixEnum Mix) : IRequest<IEnumerable<TitleProgress>>
{
}