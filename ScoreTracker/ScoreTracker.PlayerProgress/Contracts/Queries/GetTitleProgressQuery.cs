using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models.Titles;

namespace ScoreTracker.PlayerProgress.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetTitleProgressQuery(MixEnum Mix) : IQuery<IEnumerable<TitleProgress>>
{
}
