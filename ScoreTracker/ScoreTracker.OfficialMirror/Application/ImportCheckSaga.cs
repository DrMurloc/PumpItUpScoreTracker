using System.Globalization;
using System.Security.Authentication;
using MassTransit;
using MediatR;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Events;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     The completeness check: import, count what piugame says the account holds, subtract what we
///     hold, and hand the verdict to the page that asked for it.
///     <para>
///         Nothing is stored. The result lives in the page for as long as the player stays on it,
///         which is what the panel's "stay on this page" line is buying — a table, a migration and
///         a purge manifest was a great deal of machinery for remembering a sentence.
///     </para>
/// </summary>
internal sealed class ImportCheckSaga :
    IRequestHandler<StartImportCheckCommand, ImportCheckStartResult>,
    IRequestHandler<ExecuteImportCheckCommand>
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
                request.ExpectedGameTag, request.DeepScan, request.RepairBuckets), cancellationToken);
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

            await Status(request, CheckStatuses.Importing, cancellationToken);
            await _mediator.Send(new ExecuteImportCommand(request.UserId, request.Mix, request.Sid,
                request.CardId, request.ExpectedGameTag, true), cancellationToken);
            var repaired = await Repair(request, cancellationToken);

            var official = await _officialSite.GetOfficialCensus(request.Mix, request.UserId, request.Sid,
                cancellationToken);

            var charts = (await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken))
                .ToDictionary(c => c.Id);
            var records = (await _scores.GetBestScores(request.Mix, request.UserId, cancellationToken)).ToArray();
            var local = LocalCensusBuilder.Build(request.Mix, records, charts, official.Buckets.Keys.ToArray());

            var findings = await Name(request, CensusDiff.Compare(official, local), records, cancellationToken);
            var report = Report(request.Mix, official, local, records, charts, findings);

            // No storage: the panel is listening and holds this for as long as the player stays.
            await _mediator.Publish(
                new ImportCheckCompletedEvent(request.UserId, request.Mix, report, repaired),
                cancellationToken);
        }
        finally
        {
            if (deepScanSlot) _guard.EndDeepScan();
        }
    }

    private ImportCheckReport Report(MixEnum mix, AccountCensus official, AccountCensus local,
        IReadOnlyList<RecordedPhoenixScore> records, IReadOnlyDictionary<Guid, Chart> charts,
        IReadOnlyList<CensusFinding> findings)
    {
        var differences = findings.Select(f => new ImportCheckDifference(f.Bucket,
                int.TryParse(f.Bucket, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level)
                    ? level
                    : null,
                f.Kind switch
                {
                    CensusFindingKind.Missing => ImportCheckDifferenceKind.Missing,
                    CensusFindingKind.OutOfDate => ImportCheckDifferenceKind.OutOfDate,
                    _ => ImportCheckDifferenceKind.Extra
                }, f.Count,
                (f.Charts ?? Array.Empty<NamedChart>())
                .Select(c => new ImportCheckChart(c.ChartId, c.Score, c.CurrentScore)).ToArray()))
            .ToArray();

        return new ImportCheckReport(mix,
            CensusDiff.Headline(findings) == null ? ImportCheckVerdict.InSync : ImportCheckVerdict.NeedsAttention,
            official.Pumbility, LocalCensusBuilder.Pumbility(mix, records, charts),
            official.TotalPasses, local.TotalPasses, differences);
    }

    /// <summary>
    ///     Reads the levels that disagree and says WHICH charts they are. A count alone is a
    ///     support ticket; a song and a score is an answer, and it is what the player is agreeing
    ///     to when they press the repair. Costs one walk of each disagreeing level, so a clean
    ///     census never pays for it.
    /// </summary>
    private async Task<IReadOnlyList<CensusFinding>> Name(ExecuteImportCheckCommand request,
        IReadOnlyList<CensusFinding> findings, IReadOnlyList<RecordedPhoenixScore> records,
        CancellationToken cancellationToken)
    {
        var buckets = CensusDiff.BucketsToRepair(findings);
        if (buckets.Count == 0) return findings;

        await Status(request, "Finding out which charts", cancellationToken);
        var official = await _officialSite.GetBestScoresIn(request.Mix, request.UserId, request.Sid, buckets,
            false, cancellationToken);
        var held = records.Where(r => !r.IsBroken && r.Score != null).ToDictionary(r => r.ChartId);

        // Same rule the repair applies, so the list a player sees is exactly what pressing the
        // button would save — nothing extra, nothing missing.
        var namedByBucket = official
            .Where(s => BestAttemptPolicy.Beats(held.GetValueOrDefault(s.Chart.Id), s.Score,
                BestAttemptPolicy.PlateFor(s.IsBroken, s.Plate), s.IsBroken))
            .GroupBy(s => CensusBuckets.For(s.Chart.Type, s.Chart.Level, buckets.Append(CensusBuckets.CoOp).ToArray()))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<NamedChart>)g
                .Select(s => new NamedChart(s.Chart.Id, (int)s.Score,
                    // A score we already hold is what makes a row read as "behind" rather than
                    // "never imported" — the panel needs no other flag.
                    held.TryGetValue(s.Chart.Id, out var mine) ? (int)mine.Score!.Value : null))
                .ToArray(), StringComparer.Ordinal);

        return findings
            .Select(f => namedByBucket.TryGetValue(f.Bucket, out var charts) ? f with { Charts = charts } : f)
            .ToArray();
    }

    /// <summary>
    ///     Re-reads the levels the panel asked for, then lets the census below re-measure — so a
    ///     run always ends on a fresh verdict rather than the stale one that triggered it.
    ///     <para>
    ///         A deep scan passes no buckets at all, which walks the whole best list: the only way
    ///         to catch a score that improved without changing grade or plate, and the only repair
    ///         for a sub-10 residual, since Phoenix will not filter its best list below level 10.
    ///     </para>
    /// </summary>
    private async Task<int> Repair(ExecuteImportCheckCommand request, CancellationToken cancellationToken)
    {
        if (!request.DeepScan && request.RepairBuckets.Count == 0) return 0;

        var buckets = request.DeepScan ? Array.Empty<string>() : request.RepairBuckets.ToArray();
        return await _mediator.Send(new RepairScoresCommand(request.UserId, request.Mix, request.Sid,
            request.CardId, request.ExpectedGameTag, buckets, true), cancellationToken);
    }

    private Task Status(ExecuteImportCheckCommand request, string status, CancellationToken cancellationToken)
    {
        return _mediator.Publish(
            new ImportStatusUpdatedEvent(request.UserId, status, Array.Empty<RecordedPhoenixScore>(), request.Mix),
            cancellationToken);
    }
}

/// <summary>
///     Status strings the panel keys on to tell the import phase from the counting phase. The
///     check reuses the import's status channel — it is the same kind of work from the player's
///     side, and the nav pulse should light for both.
/// </summary>
internal static class CheckStatuses
{
    public const string Importing = "Importing your scores";
}
