using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.Seasons.Contracts.Queries;
using ScoreTracker.Seasons.Domain;

namespace ScoreTracker.Seasons.Application;

internal sealed class SeasonQueryHandler(ISeasonRepository seasons)
    : IRequestHandler<GetSeasonsQuery, IReadOnlyList<SeasonRecord>>
{
    public Task<IReadOnlyList<SeasonRecord>> Handle(GetSeasonsQuery request, CancellationToken cancellationToken)
    {
        return seasons.GetAll(cancellationToken);
    }
}
