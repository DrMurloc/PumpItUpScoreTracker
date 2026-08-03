using System.Globalization;
using System.Security.Authentication;
using MassTransit;
using MediatR;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     The completeness check: import, count what piugame says the account holds, subtract what we
///     hold, and store the verdict.
///     <para>
///         Feature-grouped like the rest of the mirror's sagas — the circuit-side start, the
///         background body, and the page's read all share the same dependencies.
///     </para>
/// </summary>
internal sealed class ImportCheckSaga :
    IRequestHandler<StartImportCheckCommand, ImportCheckStartResult>,
    IRequestHandler<ExecuteImportCheckCommand>,
    IRequestHandler<GetLastImportCheckQuery, LastImportCheck>
{
    private readonly IBus _bus;
    private readonly IChartRepository _charts;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IImportConcurrencyGuard _guard;
    private readonly IMediator _mediator;
    private readonly IOfficialSiteClient _officialSite;
    private readonly IImportCheckRepository _runs;
    private readonly IScoreReader _scores;

    public ImportCheckSaga(IBus bus, IChartRepository charts, ICurrentUserAccessor currentUser,
        IDateTimeOffsetAccessor dateTime, IImportConcurrencyGuard guard, IMediator mediator,
        IOfficialSiteClient officialSite, IImportCheckRepository runs, IScoreReader scores)
    {
        _bus = bus;
        _charts = charts;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _guard = guard;
        _mediator = mediator;
        _officialSite = officialSite;
        _runs = runs;
        _scores = scores;
    }

    public async Task<ImportCheckStartResult> Handle(StartImportCheckCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.User.Id;
        var spent = await _runs.CountDeepScansInMonth(userId, _dateTime.Now, cancellationToken);
        var left = DeepScanPolicy.Remaining(spent);

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

            await _bus.Publish(new RunImportCheckCommand(userId, request.Mix, sid, request.CardId,
                request.ExpectedGameTag, request.DeepScan, request.Repair), cancellationToken);
            handedOff = true;
            return new ImportCheckStartResult(ImportCheckStartOutcome.Started,
                request.DeepScan ? left - 1 : left);
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
    ///     against scores we have not fetched yet reports charts that are simply not imported yet,
    ///     which is true and useless.
    /// </summary>
    public async Task Handle(ExecuteImportCheckCommand request, CancellationToken cancellationToken)
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
                    return;
                }
            }

            // Broken bests are always fetched here: the census counts passes on both sides, so an
            // opted-out player's stage breaks change nothing it measures, while fetching them keeps
            // the repair from leaving a chart behind.
            await _mediator.Send(new ExecuteImportCommand(request.UserId, request.Mix, request.Sid,
                request.CardId, request.ExpectedGameTag, true), cancellationToken);
            await Repair(request, cancellationToken);

            var official = await _officialSite.GetOfficialCensus(request.Mix, request.UserId, request.Sid,
                cancellationToken);
            await Status(request, "Comparing your scores", cancellationToken);

            var charts = (await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken))
                .ToDictionary(c => c.Id);
            var records = (await _scores.GetBestScores(request.Mix, request.UserId, cancellationToken)).ToArray();
            var local = LocalCensusBuilder.Build(request.Mix, records, charts,
                official.Buckets.Keys.ToArray());

            var findings = CensusDiff.Compare(official, local);
            await _runs.Save(new ImportCheckRun(Guid.NewGuid(), request.UserId, request.Mix, _dateTime.Now,
                request.DeepScan ? ImportCheckKind.Deep : ImportCheckKind.Census,
                official.Pumbility, LocalCensusBuilder.Pumbility(request.Mix, records, charts),
                official.TotalPasses, local.TotalPasses, findings), cancellationToken);

            // The panel watches for this exact status and re-reads the stored verdict, the same way
            // the upload page detects a finished import.
            await Status(request, CheckStatuses.Finished, cancellationToken);
        }
        finally
        {
            if (deepScanSlot) _guard.EndDeepScan();
        }
    }

    /// <summary>
    ///     Re-reads what the last check found short, then lets the census below re-measure — so
    ///     the run always ends on a fresh verdict rather than the stale one that triggered it.
    ///     <para>
    ///         A deep scan passes no buckets at all, which walks the whole best list: the only way
    ///         to catch a score that improved without changing grade or plate, and the only repair
    ///         for a sub-10 residual, since Phoenix will not filter its best list below level 10.
    ///     </para>
    /// </summary>
    private async Task Repair(ExecuteImportCheckCommand request, CancellationToken cancellationToken)
    {
        if (!request.Repair && !request.DeepScan) return;

        IReadOnlyCollection<string> buckets = Array.Empty<string>();
        if (!request.DeepScan)
        {
            var previous = await _runs.GetLatest(request.UserId, request.Mix, cancellationToken);
            buckets = previous == null
                ? Array.Empty<string>()
                : CensusDiff.BucketsToRepair(previous.Findings);
            // Nothing localised to re-read. Falling through to an empty bucket list here would
            // silently turn a free repair into a full walk.
            if (buckets.Count == 0) return;
        }

        await _mediator.Send(new RepairScoresCommand(request.UserId, request.Mix, request.Sid, request.CardId,
            request.ExpectedGameTag, buckets, true), cancellationToken);
    }

    public async Task<LastImportCheck> Handle(GetLastImportCheckQuery request, CancellationToken cancellationToken)
    {
        var now = _dateTime.Now;
        var spent = await _runs.CountDeepScansInMonth(request.UserId, now, cancellationToken);
        var run = await _runs.GetLatest(request.UserId, request.Mix, cancellationToken);
        return new LastImportCheck(run == null ? null : ToReport(run),
            DeepScanPolicy.Remaining(spent), DeepScanPolicy.NextUnlock(now));
    }

    /// <summary>
    ///     Turns the stored findings into the page's shape. A bucket that is a single level gets
    ///     one; CO-OP, 27-and-over and the sub-10 residual do not, and the panel words those
    ///     differently.
    /// </summary>
    private static ImportCheckReport ToReport(ImportCheckRun run)
    {
        var differences = run.Findings.Select(f => new ImportCheckDifference(f.Bucket,
            int.TryParse(f.Bucket, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level)
                ? level
                : null,
            f.Kind switch
            {
                CensusFindingKind.Missing => ImportCheckDifferenceKind.Missing,
                CensusFindingKind.OutOfDate => ImportCheckDifferenceKind.OutOfDate,
                _ => ImportCheckDifferenceKind.Extra
            }, f.Count)).ToArray();

        var verdict = CensusDiff.Headline(run.Findings) switch
        {
            CensusFindingKind.Missing => ImportCheckVerdict.MissingScores,
            CensusFindingKind.OutOfDate => ImportCheckVerdict.OutOfDateScores,
            CensusFindingKind.Extra => ImportCheckVerdict.AheadOfSite,
            _ => ImportCheckVerdict.InSync
        };

        return new ImportCheckReport(run.Mix, run.RanAt, verdict, run.OfficialPumbility, run.LocalPumbility,
            run.OfficialPasses, run.LocalPasses, differences);
    }

    private Task Status(ExecuteImportCheckCommand request, string status, CancellationToken cancellationToken)
    {
        return _mediator.Publish(
            new ImportStatusUpdatedEvent(request.UserId, status, Array.Empty<RecordedPhoenixScore>(), request.Mix),
            cancellationToken);
    }
}

/// <summary>
///     Status strings the panel keys on. The check reuses the import's status channel — it is the
///     same kind of work from the player's side, and the nav pulse should light for both.
/// </summary>
internal static class CheckStatuses
{
    public const string Finished = "Score check finished";
}
