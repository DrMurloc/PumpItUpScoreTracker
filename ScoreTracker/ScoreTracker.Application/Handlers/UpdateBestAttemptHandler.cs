using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class UpdateBestAttemptHandler : IRequestHandler<UpdateBestAttemptCommand>
{
    private readonly IChartAttemptRepository _attempts;
    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _dateTimeOffset;
    private readonly ICurrentUserAccessor _user;

    public UpdateBestAttemptHandler(
        IChartAttemptRepository attempts,
        ICurrentUserAccessor user,
        IDateTimeOffsetAccessor dateTimeOffset,
        IChartRepository charts)
    {
        _charts = charts;
        _attempts = attempts;
        _user = user;
        _dateTimeOffset = dateTimeOffset;
    }

    public async Task<Unit> Handle(UpdateBestAttemptCommand request, CancellationToken cancellationToken)
    {
        var chart = await _charts.GetChart(request.SongName, request.ChartType, request.Level, cancellationToken);
        if (request.LetterGrade != null)
            await _attempts.SetBestAttempt(_user.User.Id, chart,
                new ChartAttempt(request.LetterGrade.Value, request.IsBroken),
                _dateTimeOffset.Now, cancellationToken);
        else
            await _attempts.RemoveBestAttempt(_user.User.Id, chart, cancellationToken);

        return Unit.Value;
    }
}