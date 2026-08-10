using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     Reads and clears the one-time notice for a run the process abandoned
///     (docs/design/import-restart-recovery.md §7).
/// </summary>
internal sealed class ImportInterruptionHandler
    : IRequestHandler<GetUnacknowledgedInterruptedImportQuery, ImportAttemptRecord?>,
        IRequestHandler<AcknowledgeImportInterruptionCommand>
{
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IImportResultRepository _results;

    public ImportInterruptionHandler(IImportResultRepository results, IDateTimeOffsetAccessor dateTime)
    {
        _results = results;
        _dateTime = dateTime;
    }

    public Task<ImportAttemptRecord?> Handle(GetUnacknowledgedInterruptedImportQuery request,
        CancellationToken cancellationToken)
    {
        return _results.GetUnacknowledgedInterrupted(request.UserId, cancellationToken);
    }

    public Task Handle(AcknowledgeImportInterruptionCommand request, CancellationToken cancellationToken)
    {
        return _results.Acknowledge(request.ImportResultId, _dateTime.Now, cancellationToken);
    }
}
