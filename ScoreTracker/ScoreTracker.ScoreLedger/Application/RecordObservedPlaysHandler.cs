using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     Journals plays that never became a record, so a chart's history holds the attempts and
///     not only the improvements — the recent window's losing runs, and the stage breaks from
///     both surfaces. Writes nothing to the ledger's record — that is
///     <see cref="UpdatePhoenixRecordHandler" />'s job, and a play arriving through both paths
///     collapses onto one row by its play key.
/// </summary>
internal sealed class RecordObservedPlaysHandler(IScoreJournalRepository journal, IMemoryCache cache,
        IChartRepository charts, SeasonalBestWriter seasonalBests, ILogger<RecordObservedPlaysHandler> logger)
    : IRequestHandler<RecordObservedPlaysCommand>
{
    public async Task Handle(RecordObservedPlaysCommand request, CancellationToken cancellationToken)
    {
        // A walk-off is never stored, whether or not it would have been a best.
        var plays = request.Plays
            .Where(p => !BestAttemptPolicy.IsWalkOff(p.IsBroken, p.Score, p.Judgements))
            .ToArray();
        if (plays.Length == 0) return;

        // One catalog read per chart, for the combo, the tripwire and the stage-break solver.
        // Charts appear many times in a window (the same song replayed all evening) so this is
        // well under one per row.
        var facts = new Dictionary<Guid, ChartFacts>();
        foreach (var chartId in plays.Where(p => p.Judgements != null).Select(p => p.ChartId).Distinct())
            facts[chartId] = await NoteCountWatch.FactsFor(charts, request.Mix, chartId, cancellationToken);

        // One line per chart, not per play: a window holds several runs of the same song, and a
        // drifted catalog would otherwise repeat itself inside a single import.
        var warned = new HashSet<Guid>();
        var entries = plays.Select(p =>
            {
                var chart = facts.GetValueOrDefault(p.ChartId);
                if (warned.Add(p.ChartId))
                    NoteCountWatch.WarnOnDisagreement(logger, request.Mix, p.ChartId, p.Judgements, chart.NoteCount,
                        p.IsBroken, p.IsStageBroken);
                // A stage break is broken by definition and never scored: the running number the
                // site prints for one is not a chart score, and the plate is null on any break.
                var isBroken = p.IsBroken || p.IsStageBroken;
                var score = p.IsStageBroken ? null : p.Score;
                return new ScoreJournalEntry(p.PlayedAt, request.Source, request.UserId, p.ChartId,
                    score, BestAttemptPolicy.PlateFor(isBroken, p.Plate), isBroken, request.Mix,
                    request.SessionId, PhoenixComboSolver.WithMaxCombo(p.Judgements, score, chart.NoteCount), false,
                    IsStageBroken: p.IsStageBroken,
                    Cause: NoteCountWatch.CauseFor(p.IsStageBroken, p.Judgements, chart, request.Mix));
            })
            .ToArray();

        await journal.AppendObservations(entries, cancellationToken);

        // A season's pool starts empty, so a run below the player's all-time best is still that
        // season's best on the chart — and these are the only rows those runs ever produce, since
        // the import filtered them out before the record handler saw them (seasons.md §4.1). Judged
        // on their own play time and nothing else: none of them raised an all-time record.
        // A break is journaled regardless, but it only becomes a BEST for a player who opted in --
        // the same rule the all-time best list applies. Without this a player who unticked the
        // setting collects broken seasonal bests on every chart they have not passed in-window, and
        // per D38 each one counts the chart as played for their seasonal folder completion.
        await seasonalBests.Write(request.Mix, request.UserId, request.Source,
            entries.Where(e => request.IncludeBroken || !e.IsBroken)
                .Select(e => new SeasonalBestWriter.Candidate(
                new RecordedPhoenixScore(e.ChartId, e.Score, e.Plate, e.IsBroken, e.OccurredAt, e.Source,
                    e.Judgements),
                    RaisedExistingRecord: false, e.IsStageBroken)).ToArray(),
            cancellationToken);

        // A replay can settle a run the session recorded earlier, so every chart this write gave a
        // judged stage break is re-solved against the whole session.
        if (request.SessionId is { } sessionId)
        {
            var replayed = entries.Where(e => e.IsStageBroken && e.Judgements != null)
                .Select(e => e.ChartId)
                .Distinct()
                .ToDictionary(chartId => chartId, chartId => facts[chartId]);
            if (replayed.Count > 0)
                await SessionStageBreaks.Resolve(journal, request.UserId, request.Mix, sessionId, replayed,
                    cancellationToken);
        }

        // The limbo board reads exactly these rows, so it goes stale exactly here. Evicted AFTER
        // the write, which is why this is the hook rather than ScoreImportCompletedEvent — that
        // one is published before the rows it describes exist, and only for official imports
        // (docs/design/limbo-leaderboard.md §5). Remove on an absent key is a no-op, so no chart
        // needs checking against the flag set first.
        foreach (var chartId in entries.Select(e => e.ChartId).Distinct())
            cache.Remove(LedgerCacheKeys.LimboBoard(request.Mix, chartId));
    }
}
