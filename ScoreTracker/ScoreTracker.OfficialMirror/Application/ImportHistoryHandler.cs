using MediatR;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     A player's recent import attempts, straight off the vertical's own table.
///     <para>
///         The score count is stamped on the run when it closes rather than read off the Ledger's
///         <c>ScoreSession.ScoreCount</c>, which counts only the new passes and upscores the
///         announcement carried, and which a run cut short by a restart only gets from the
///         startup replay.
///     </para>
/// </summary>
internal sealed class ImportHistoryHandler
    : IRequestHandler<GetImportHistoryQuery, IReadOnlyList<ImportAttemptRecord>>
{
    private readonly IImportResultRepository _results;

    public ImportHistoryHandler(IImportResultRepository results)
    {
        _results = results;
    }

    public Task<IReadOnlyList<ImportAttemptRecord>> Handle(GetImportHistoryQuery request,
        CancellationToken cancellationToken)
    {
        return _results.GetRecent(request.UserId, request.Take, cancellationToken);
    }
}
