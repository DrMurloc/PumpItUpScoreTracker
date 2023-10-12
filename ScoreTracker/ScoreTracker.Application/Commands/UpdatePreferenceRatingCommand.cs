using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    public sealed record UpdatePreferenceRatingCommand
        (MixEnum Mix, Guid ChartId, Rating Rating) : IRequest<ChartPreferenceRatingRecord>
    {
    }
}
