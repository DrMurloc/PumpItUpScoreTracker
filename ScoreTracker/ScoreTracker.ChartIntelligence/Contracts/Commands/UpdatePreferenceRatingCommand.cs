using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record UpdatePreferenceRatingCommand
        (MixEnum Mix, Guid ChartId, PreferenceRating Rating) : IRequest<ChartPreferenceRatingRecord>
    {
    }
}
