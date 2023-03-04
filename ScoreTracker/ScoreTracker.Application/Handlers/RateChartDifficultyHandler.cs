using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class
    RateChartDifficultyHandler : IRequestHandler<RateChartDifficultyCommand, ChartDifficultyRatingRecord>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IChartDifficultyRatingRepository _difficultyRatings;

    private readonly IMediator _mediator;

    public RateChartDifficultyHandler(IChartDifficultyRatingRepository difficultyRatings,
        ICurrentUserAccessor currentUser, IMediator mediator)
    {
        _difficultyRatings = difficultyRatings;
        _currentUser = currentUser;
        _mediator = mediator;
    }

    public async Task<ChartDifficultyRatingRecord> Handle(RateChartDifficultyCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.User.Id;
        await _difficultyRatings.RateChart(request.ChartId, userId, request.Rating, cancellationToken);

        return await _mediator.Send(new ReCalculateChartRatingCommand(request.ChartId), cancellationToken);
    }
}