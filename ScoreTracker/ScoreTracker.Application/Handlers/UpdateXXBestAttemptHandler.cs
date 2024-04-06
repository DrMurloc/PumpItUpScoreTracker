using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class UpdateXXBestAttemptHandler : IRequestHandler<UpdateXXBestAttemptCommand>
{
    private readonly IXXChartAttemptRepository _attempts;
    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _dateTimeOffset;
    private readonly ICurrentUserAccessor _user;

    public UpdateXXBestAttemptHandler(
        IXXChartAttemptRepository attempts,
        ICurrentUserAccessor user,
        IDateTimeOffsetAccessor dateTimeOffset,
        IChartRepository charts)
    {
        _charts = charts;
        _attempts = attempts;
        _user = user;
        _dateTimeOffset = dateTimeOffset;
    }

    public async Task Handle(UpdateXXBestAttemptCommand request, CancellationToken cancellationToken)
    {
        var chart = await _charts.GetChart(MixEnum.XX, request.chartId, cancellationToken);
        if (request.LetterGrade != null)
            await _attempts.SetBestAttempt(_user.User.Id, chart,
                new XXChartAttempt(request.LetterGrade.Value, request.IsBroken, request.Score, _dateTimeOffset.Now),
                _dateTimeOffset.Now, cancellationToken);
        else
            await _attempts.RemoveBestAttempt(_user.User.Id, chart, cancellationToken);

        
    }
}