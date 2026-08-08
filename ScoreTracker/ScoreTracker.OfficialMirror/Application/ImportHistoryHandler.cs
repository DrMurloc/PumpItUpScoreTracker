using MediatR;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.ScoreLedger.Contracts.Queries;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     Reads a player's import attempts and fills in how many scores each one saved.
///     <para>
///         The count lives on the Ledger's session, not here, and a vertical never joins onto
///         another's tables (ADR-001) — so it arrives through the published GetScoreSessionsQuery
///         and is matched on the session id the run recorded. A run with no session, or one whose
///         session predates this table, keeps a null count, which the page prints as "—" rather
///         than as a confident zero.
///     </para>
/// </summary>
internal sealed class ImportHistoryHandler
    : IRequestHandler<GetImportHistoryQuery, IReadOnlyList<ImportAttemptRecord>>
{
    private readonly IMediator _mediator;
    private readonly IImportResultRepository _results;

    public ImportHistoryHandler(IImportResultRepository results, IMediator mediator)
    {
        _results = results;
        _mediator = mediator;
    }

    public async Task<IReadOnlyList<ImportAttemptRecord>> Handle(GetImportHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var attempts = await _results.GetRecent(request.UserId, request.Take, cancellationToken);
        if (attempts.Count == 0) return attempts;

        var wanted = attempts.Where(a => a.SessionId is not null).Select(a => a.SessionId!.Value).ToHashSet();
        if (wanted.Count == 0) return attempts;

        var sessions = (await _mediator.Send(new GetScoreSessionsQuery(request.UserId), cancellationToken))
            .Where(s => wanted.Contains(s.Id))
            .ToDictionary(s => s.Id, s => s.ScoreCount);

        return attempts
            .Select(a => a.SessionId is { } id && sessions.TryGetValue(id, out var count)
                ? a with { ScoreCount = count }
                : a)
            .ToArray();
    }
}
