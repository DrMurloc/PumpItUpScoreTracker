using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     Writes the seasonal half of a personal best (docs/design/seasons.md §4.1). Both official
///     paths hand their scores here — the best-list cards <see cref="UpdatePhoenixRecordHandler" />
///     seats, and the recently-played runs <see cref="RecordObservedPlaysHandler" /> journals — so
///     the counting rule is applied once, in one place, and the two paths cannot drift.
///     <para>
///         The second path is the one that matters: a season's pool starts empty, so a run well
///         below a player's all-time best is still that season's best on the chart. Those runs never
///         reach the record handler (the import filters them out before it), and exist for us at all
///         only while they sit on the fifty-card recent list.
///     </para>
///     <para>
///         Two entry points, because a live import and the backfill differ in exactly one thing:
///         <see cref="Write" /> asks the counting rule which season a play belongs to, and
///         <see cref="WriteInto" /> is told. Everything after that decision — the stage-break gate,
///         the winner per chart, the comparison against the row already standing — is one body both
///         run, which is what keeps a backfilled season and a tracked one the same rows.
///     </para>
/// </summary>
internal sealed class SeasonalBestWriter(IPhoenixRecordRepository records, ISeasonReader seasons,
    IDateTimeOffsetAccessor dateTime)
{
    /// <summary>
    ///     One observed score, whether it raised an all-time record the player already held — the
    ///     two facts <see cref="SeasonCountingPolicy" /> judges it on — and whether the stage broke,
    ///     which is never a best on any pool.
    /// </summary>
    public readonly record struct Candidate(RecordedPhoenixScore Score, bool RaisedExistingRecord,
        bool IsStageBroken = false);

    /// <summary>
    ///     The live path: every candidate is routed by the counting rule, which under D13 can only
    ///     ever answer with the season running now.
    /// </summary>
    public async Task Write(MixEnum mix, Guid userId, string source, IReadOnlyList<Candidate> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0) return;
        // Phoenix 2 only (docs/design/seasons.md §1). The read side — the rollup, the backfill — has
        // always skipped Phoenix 1, so a row written here for it would be an orphan nothing ever
        // recomputes: never rolled up, never replayed, drifting from the journal for good. Phoenix 1
        // is also the larger importing population, so the gate belongs at the write, not just the read.
        if (!mix.HasSeasons()) return;
        // Before the first roll there is no season to write to, which is every deploy's first
        // hours and every test that never opened one.
        var calendar = await seasons.GetSeasons(cancellationToken);
        if (calendar.Count == 0) return;

        var now = dateTime.Now;
        await Persist(mix, userId, candidates,
            c => SeasonCountingPolicy.SeasonFor(source, c.Score.RecordedDate, c.RaisedExistingRecord, calendar, now),
            cancellationToken);
    }

    /// <summary>
    ///     The backfill path (D23): the season is named rather than inferred, because a rebuild is
    ///     replaying a window that has already closed — and the counting rule, correctly, will not
    ///     write a closed season for a live import (D13). The rows the caller hands over are already
    ///     official-import plays inside that window; the journal read is what guarantees it.
    /// </summary>
    public async Task WriteInto(MixEnum mix, Guid userId, SeasonId season, IReadOnlyList<Candidate> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0 || !mix.HasSeasons()) return;
        await Persist(mix, userId, candidates, _ => season, cancellationToken);
    }

    private async Task Persist(MixEnum mix, Guid userId, IReadOnlyList<Candidate> candidates,
        Func<Candidate, SeasonId?> route, CancellationToken cancellationToken)
    {
        // A recent window holds several runs of one chart, so the winner per (season, chart) is
        // settled in memory first: one read and at most one write each, not one per run.
        var best = new Dictionary<(short Season, Guid Chart), RecordedPhoenixScore>();
        foreach (var candidate in candidates)
        {
            var score = candidate.Score;
            // A stage break is never a best, on any pool; nor is a run the site never scored.
            if (!BestAttemptPolicy.CanBeRecord(candidate.IsStageBroken) || score.Score == null) continue;
            var season = route(candidate);
            if (season == null) continue;

            var key = (season.Value.Value, score.ChartId);
            if (!best.TryGetValue(key, out var standing) ||
                BestAttemptPolicy.Beats(standing, score.Score, score.Plate, score.IsBroken))
                best[key] = score;
        }

        foreach (var ((season, chartId), score) in best)
        {
            var seasonId = SeasonId.From(season);
            var stored = await records.GetRecordedScore(mix, userId, chartId, seasonId, cancellationToken);
            if (!BestAttemptPolicy.Beats(stored, score.Score, score.Plate, score.IsBroken)) continue;
            await records.UpdateBestAttempt(mix, userId, score, seasonId, cancellationToken);
        }
    }
}
