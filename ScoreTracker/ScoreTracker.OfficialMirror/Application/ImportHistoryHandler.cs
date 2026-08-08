using MediatR;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     A player's recent import attempts, straight off the vertical's own table.
///     <para>
///         The score count is stamped on the run when it closes rather than read off the Ledger's
///         <c>ScoreSession.ScoreCount</c>. That counter is written when the score batch DRAINS —
///         a ~2 minute in-memory debounce — so it cannot answer for a run that just finished, and
///         an app restart inside the window leaves it at zero permanently while the journal holds
///         the rows. Observed 2026-08-08: a check that saved seven scores sat at ScoreCount 0 with
///         seven journal rows behind it.
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
