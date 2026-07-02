using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record UpdatePreferenceRatingCommand
        (MixEnum Mix, Guid ChartId, PreferenceRating Rating) : IRequest<ChartPreferenceRatingRecord>
    {
    }
}
