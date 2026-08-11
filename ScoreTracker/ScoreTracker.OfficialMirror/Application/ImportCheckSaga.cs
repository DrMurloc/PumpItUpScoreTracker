using System.Security.Authentication;
using MassTransit;
using MediatR;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Events;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     The completeness check: import, work out which levels piugame disagrees on, re-read those,
///     and save whatever beats what we hold.
///     <para>
///         Anything it finds is saved on the spot as a normal import — same session, same journal,
///         same rating recalculation — so the scores land on the player's sessions page and their
///         Discord card like any other. Nobody is asked to approve their own score from the
///         official site, which is why this hands back a count rather than a verdict, and why
///         leaving the page costs nothing.
///     </para>
/// </summary>
internal sealed class ImportCheckSaga :
    IRequestHandler<StartImportCheckCommand, ImportCheckStartResult>,
    IRequestHandler<ExecuteImportCheckCommand, ImportCheckRun>
{
    private readonly IBus _bus;
    private readonly IChartRepository _charts;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IImportConcurrencyGuard _guard;
    private readonly IMediator _mediator;
    private readonly IOfficialSiteClient _officialSite;
    private readonly IScoreReader _scores;

    public ImportCheckSaga(IBus bus, IChartRepository charts, ICurrentUserAccessor currentUser,
        IImportConcurrencyGuard guard, IMediator mediator, IOfficialSiteClient officialSite, IScoreReader scores)
    {
        _bus = bus;
        _charts = charts;
        _currentUser = currentUser;
        _guard = guard;
        _mediator = mediator;
        _officialSite = officialSite;
        _scores = scores;
    }

    public async Task<ImportCheckStartResult> Handle(StartImportCheckCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.User.Id;
        var left = await _mediator.Send(new GetDeepScansRemainingQuery(userId), cancellationToken);

        if (request.DeepScan && left == 0)
            return new ImportCheckStartResult(ImportCheckStartOutcome.NoDeepScansLeft, 0);
        if (!_guard.TryBegin(userId))
            return new ImportCheckStartResult(ImportCheckStartOutcome.AlreadyRunning, left);

        // The slot is held until the background job releases it; only the pre-flight failures
        // below hand it back.
        var handedOff = false;
        try
        {
            var credentials = await Resolve(request.Source, cancellationToken);
            if (credentials == null)
                return new ImportCheckStartResult(ImportCheckStartOutcome.CredentialUnlockFailed, left);

            string sid;
            try
            {
                sid = await _officialSite.SignIn(request.Mix, credentials.Value.Username,
                    credentials.Value.Password, cancellationToken);
            }
            catch (InvalidCredentialException)
            {
                return new ImportCheckStartResult(ImportCheckStartOutcome.InvalidCredentials, left);
            }

            // Spent only once the run is certain to start, so a mistyped password costs nothing.
            // The decrement is atomic in the database, which is what stops a second tab spending
            // the same last scan.
            if (request.DeepScan)
            {
                if (!await _mediator.Send(new SpendDeepScanCommand(userId), cancellationToken))
                    return new ImportCheckStartResult(ImportCheckStartOutcome.NoDeepScansLeft, 0);
                left--;
            }

            await _bus.Publish(new RunImportCheckCommand(userId, request.Mix, sid, request.CardId,
                request.ExpectedGameTag, request.DeepScan, request.IncludeBroken), cancellationToken);
            handedOff = true;
            return new ImportCheckStartResult(ImportCheckStartOutcome.Started, left);
        }
        finally
        {
            if (!handedOff) _guard.End(userId);
        }
    }

    private async Task<(string Username, string Password)?> Resolve(ImportCredentialSource source,
        CancellationToken cancellationToken)
    {
        switch (source)
        {
            case TypedCredentialSource typed:
                return (typed.Username, typed.Password);
            case StoredCredentialSource stored:
                var revealed = await _mediator.Send(
                    new RevealImportCredentialQuery(stored.KeyId, stored.Ciphertext), cancellationToken);
                return revealed == null ? null : (revealed.Username, revealed.Password);
            default:
                return null;
        }
    }

    /// <summary>
    ///     The background body. Imports FIRST — counting an account that played twenty minutes ago
    ///     against scores we have not fetched yet reports charts that are simply not imported yet.
    /// </summary>
    public async Task<ImportCheckRun> Handle(ExecuteImportCheckCommand request, CancellationToken cancellationToken)
    {
        var deepScanSlot = false;
        try
        {
            if (request.DeepScan)
            {
                deepScanSlot = _guard.TryBeginDeepScan();
                if (!deepScanSlot)
                {
                    await Status(request, "Another deep scan is running — try again in a few minutes",
                        cancellationToken);
                    return new ImportCheckRun(null, 0);
                }
            }

            // ONE session for the whole run, shared by the import and by whatever the deeper read
            // recovers. Letting each mint its own put two rows seconds apart in the player's
            // sessions list for a single button press.
            var sessionId = await _mediator.Send(new BeginScoreSessionCommand(request.UserId, request.Mix,
                    ScoreJournalEntry.OfficialImportSource, request.ExpectedGameTag, request.CardId),
                cancellationToken);

            // The player's choice, not a literal: this half writes records, so a hardcoded true
            // re-recorded every break somebody had just cleaned up — press Score check and they
            // were all back. Both halves of the run read the same flag.
            var imported = await _mediator.Send(new ExecuteImportCommand(request.UserId, request.Mix, request.Sid,
                request.CardId, request.ExpectedGameTag, request.IncludeBroken, sessionId), cancellationToken);

            var (added, checkedCount) = request.DeepScan
                ? await DeepScan(request, sessionId, cancellationToken)
                : await Census(request, sessionId, cancellationToken);

            await _mediator.Publish(
                new ImportCheckCompletedEvent(request.UserId, request.Mix, added, checkedCount),
                cancellationToken);
            // Both halves count: the run is one press, and the import inside it saved into the
            // same session as the repair that followed.
            return new ImportCheckRun(sessionId, imported + added);
        }
        finally
        {
            if (deepScanSlot) _guard.EndDeepScan();
        }
    }

    /// <summary>
    ///     Counts every level, then re-reads only the ones that disagree. A clean account pays for
    ///     the census and nothing else.
    /// </summary>
    private async Task<(int Added, int Checked)> Census(ExecuteImportCheckCommand request, Guid sessionId,
        CancellationToken cancellationToken)
    {
        var official = await _officialSite.GetOfficialCensus(request.Mix, request.UserId, request.Sid,
            cancellationToken);

        var charts = (await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);
        var records = (await _scores.GetBestScores(request.Mix, request.UserId, cancellationToken)).ToArray();
        var local = LocalCensusBuilder.Build(request.Mix, records, charts, official.Buckets.Keys.ToArray());

        var buckets = CensusDiff.BucketsToRepair(CensusDiff.Compare(official, local));
        if (buckets.Count == 0) return (0, official.TotalPasses);

        await Status(request, "Reading the levels that don't match", cancellationToken);
        return (await Save(request, buckets, sessionId, cancellationToken), official.TotalPasses);
    }

    /// <summary>
    ///     Walks the whole best-score list, no census first: the walk finds everything a count
    ///     would have pointed at, plus the one thing a count never could — a score improved without
    ///     changing grade or plate.
    /// </summary>
    private async Task<(int Added, int Checked)> DeepScan(ExecuteImportCheckCommand request, Guid sessionId,
        CancellationToken cancellationToken)
    {
        var found = await _officialSite.GetBestScoresIn(request.Mix, request.UserId, request.Sid,
            Array.Empty<string>(), request.IncludeBroken, cancellationToken);
        return (await SaveFound(request, found, sessionId, cancellationToken), found.Count);
    }

    private async Task<int> Save(ExecuteImportCheckCommand request, IReadOnlyCollection<string> buckets,
        Guid sessionId, CancellationToken cancellationToken)
    {
        var found = await _officialSite.GetBestScoresIn(request.Mix, request.UserId, request.Sid, buckets,
            request.IncludeBroken, cancellationToken);
        return await SaveFound(request, found, sessionId, cancellationToken);
    }

    // Through the import's own save path, so a recovered score obeys the same raise-only rule and
    // joins the same session, journal and rating sweep as any other imported one.
    private Task<int> SaveFound(ExecuteImportCheckCommand request,
        IReadOnlyList<OfficialRecordedScore> found, Guid sessionId,
        CancellationToken cancellationToken)
    {
        return _mediator.Send(new SaveOfficialScoresCommand(request.UserId, request.Mix, sessionId, found),
            cancellationToken);
    }

    private Task Status(ExecuteImportCheckCommand request, string status, CancellationToken cancellationToken)
    {
        return _mediator.Publish(
            new ImportStatusUpdatedEvent(request.UserId, status, Array.Empty<RecordedPhoenixScore>(), request.Mix),
            cancellationToken);
    }
}
