using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class RecordAttemptHandler : IRequestHandler<RecordAttemptCommand, bool>
{
    private readonly IChartAttemptRepository _attempts;
    private readonly IChartRepository _charts;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDateTimeOffsetAccessor _dateTimeOffset;

    public RecordAttemptHandler(IChartAttemptRepository attempts,
        ICurrentUserAccessor currentUser,
        IDateTimeOffsetAccessor dateTimeOffset,
        IChartRepository charts)
    {
        _charts = charts;
        _attempts = attempts;
        _currentUser = currentUser;
        _dateTimeOffset = dateTimeOffset;
    }

    public async Task<bool> Handle(RecordAttemptCommand request, CancellationToken cancellationToken)
    {
        var chart = await _charts.GetChart(request.SongName, request.ChartType, request.DifficultyLevel,
            cancellationToken);
        var userId = _currentUser.User.Id;
        var now = _dateTimeOffset.Now;


        var newAttempt = new ChartAttempt(request.Grade, request.IsBroken);
        var bestAttempt = await _attempts.GetBestAttempt(userId, chart, cancellationToken);

        var isBetterAttempt = bestAttempt == null || newAttempt > bestAttempt;
        if (isBetterAttempt)
            await _attempts.SetBestAttempt(userId, chart, newAttempt, now, cancellationToken);

        return isBetterAttempt;
    }
}