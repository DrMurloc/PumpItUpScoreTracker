using MediatR;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;

namespace ScoreTracker.OfficialMirror.Application;

internal sealed class ImportInProgressHandler : IRequestHandler<GetImportInProgressQuery, bool>
{
    private readonly IImportConcurrencyGuard _guard;

    public ImportInProgressHandler(IImportConcurrencyGuard guard)
    {
        _guard = guard;
    }

    public Task<bool> Handle(GetImportInProgressQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_guard.IsRunning(request.UserId, request.Mix));
    }
}
