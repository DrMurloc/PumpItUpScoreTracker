using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Application;

internal sealed class GetCrossMixPassesHandler : IRequestHandler<GetCrossMixPassesQuery, IReadOnlySet<Guid>>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IPhoenixRecordRepository _records;
    private readonly IUserAccessService _userAccess;
    private readonly IXXChartAttemptRepository _xxAttempts;

    public GetCrossMixPassesHandler(IPhoenixRecordRepository records, IXXChartAttemptRepository xxAttempts,
        ICurrentUserAccessor currentUser, IUserAccessService userAccess)
    {
        _records = records;
        _xxAttempts = xxAttempts;
        _currentUser = currentUser;
        _userAccess = userAccess;
    }

    public async Task<IReadOnlySet<Guid>> Handle(GetCrossMixPassesQuery request,
        CancellationToken cancellationToken)
    {
        var userId = request.UserId ?? _currentUser.User.Id;
        if (!await _userAccess.HasAccessTo(userId, cancellationToken))
            return new HashSet<Guid>();

        var passes = new HashSet<Guid>();
        // Phoenix-family mixes live in the phoenix records store; mixes without rows
        // (including future legacy-mix enum values) just contribute nothing.
        foreach (var mix in Enum.GetValues<MixEnum>().Where(m => m != request.ExcludingMix && m != MixEnum.XX))
            passes.UnionWith((await _records.GetRecordedScores(mix, userId, cancellationToken))
                .Where(r => !r.IsBroken)
                .Select(r => r.ChartId));

        if (request.ExcludingMix != MixEnum.XX)
            passes.UnionWith((await _xxAttempts.GetBestAttempts(userId, cancellationToken))
                .Where(a => !(a.BestAttempt?.IsBroken ?? true))
                .Select(a => a.Chart.Id));

        return passes;
    }
}
