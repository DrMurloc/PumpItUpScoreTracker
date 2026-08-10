using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Application;

internal sealed class UpdateXXBestAttemptHandler : IRequestHandler<UpdateXXBestAttemptCommand>
{
    private readonly IXXChartAttemptRepository _attempts;
    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _dateTimeOffset;
    private readonly IScoreJournalRepository _journal;
    private readonly ICurrentUserAccessor _user;

    public UpdateXXBestAttemptHandler(
        IXXChartAttemptRepository attempts,
        ICurrentUserAccessor user,
        IDateTimeOffsetAccessor dateTimeOffset,
        IChartRepository charts,
        IScoreJournalRepository journal)
    {
        _charts = charts;
        _attempts = attempts;
        _user = user;
        _dateTimeOffset = dateTimeOffset;
        _journal = journal;
    }

    public async Task Handle(UpdateXXBestAttemptCommand request, CancellationToken cancellationToken)
    {
        // The chart is materialized for the requested mix, so the repository keys the
        // attempt per (user, chart, mix) — each legacy mix gets its own best.
        var chart = await _charts.GetChart(request.Mix, request.chartId, cancellationToken);
        var userId = _user.User.Id;
        if (request.LetterGrade == null)
        {
            await _attempts.RemoveBestAttempt(userId, chart, cancellationToken);
            return;
        }

        var now = _dateTimeOffset.Now;
        var incoming = new XXChartAttempt(request.LetterGrade.Value, request.IsBroken, request.Score, now);

        var stored = request.KeepBestStats
            ? await _attempts.GetBestAttempt(userId, chart, cancellationToken)
            : null;

        // An acquisition source may only ever raise a record, and raises the two axes
        // independently: a run that beats the score but not the grade keeps the old grade,
        // and the reverse keeps the old score (LegacyBestAttemptPolicy). The manual routes
        // pass KeepBestStats false and overwrite, because a correction has to be able to
        // lower a record.
        var best = request.KeepBestStats
            ? LegacyBestAttemptPolicy.Merge(stored, incoming, now)
            : incoming;

        // A submission that moves neither axis is noise, not history — imports re-see the same
        // play constantly. It must not touch the record, the journal, or the recorded date.
        if (request.KeepBestStats && ReferenceEquals(best, stored)) return;

        await _attempts.SetBestAttempt(userId, chart, best, now, cancellationToken);

        // The journal is the record's history, and now legacy has one too: it gets the
        // resulting best-attempt state, exactly and only when that state changes. The number
        // and the letter ride the legacy fields — an era score is not a PhoenixScore and most
        // of them are far above its ceiling.
        await _journal.Append(new ScoreJournalEntry(best.RecordedOn, request.Source, userId,
                request.chartId, null, null, best.IsBroken, request.Mix,
                LegacyScore: best.Score, LegacyGrade: best.LetterGrade),
            cancellationToken);
    }
}
