using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Application
{
    /// <summary>
    ///     The PUMBILITY page's reads: the pool with its bar, the targets, and the Phoenix 2
    ///     carryover (docs/design/pumbility-overhaul.md §3, §5). One dispatch per section
    ///     rather than the page assembling itself from six.
    /// </summary>
    internal sealed class PumbilityPageSaga :
        IRequestHandler<GetPumbilityPageQuery, PumbilityPageRecord>,
        IRequestHandler<ProjectPhoenix2CarryoverQuery, Phoenix2CarryoverRecord>
    {
        /// <summary>The pool is the top fifty. Everything on the page measures against its floor.</summary>
        private const int PoolSize = 50;

        /// <summary>How many charts just outside the pool the curve ghosts in.</summary>
        private const int WaitingRoomSize = 6;

        /// <summary>
        ///     How deep the Phoenix 1 repricing is kept. The pool is the first fifty; the rest
        ///     are suggestion candidates, and the depth only has to outrun the target cap after
        ///     already-scored and unavailable charts are filtered out.
        /// </summary>
        private const int CandidateDepth = 200;

        /// <summary>
        ///     How long the suggestion list runs, per source and again after the merge (owner,
        ///     2026-08-06). Nobody plans past the first hundred, and the tail is payload and
        ///     scrolling for rows no one reads.
        /// </summary>
        private const int MaxTargets = 100;

        private readonly IMediator _mediator;
        private readonly IScoreReader _scores;

        public PumbilityPageSaga(IMediator mediator, IScoreReader scores)
        {
            _mediator = mediator;
            _scores = scores;
        }

        public async Task<PumbilityPageRecord> Handle(GetPumbilityPageQuery request,
            CancellationToken cancellationToken)
        {
            var mix = request.Mix;
            var charts = (await _mediator.Send(new GetChartsQuery(mix), cancellationToken))
                .ToDictionary(c => c.Id);
            var scoring = ScoringConfiguration.PumbilityScoring(mix, false);

            var ranked = (await _mediator.Send(
                    new GetTop50ForPlayerQuery(request.UserId, request.Pool, PoolSize + WaitingRoomSize, mix),
                    cancellationToken))
                .Where(s => s.Score != null && charts.ContainsKey(s.ChartId))
                .Select(s => (Score: s, Value: (int)scoring.GetScore(charts[s.ChartId], s.Score!.Value,
                    s.Plate ?? PhoenixPlate.RoughGame, s.IsBroken)))
                .OrderByDescending(x => x.Value)
                .ToArray();

            var pool = ranked.Take(PoolSize)
                .Select((x, i) => new PoolEntry(i + 1, x.Score.ChartId, x.Score.Score!.Value, x.Score.Plate,
                    x.Score.IsBroken, x.Score.RecordedDate, x.Value))
                .ToArray();
            var waiting = ranked.Skip(PoolSize).Take(WaitingRoomSize)
                .Select((x, i) => new PoolEntry(PoolSize + i + 1, x.Score.ChartId, x.Score.Score!.Value,
                    x.Score.Plate, x.Score.IsBroken, x.Score.RecordedDate, x.Value))
                .ToArray();

            // Until the pool holds fifty, a new chart displaces nothing and contributes whole.
            var full = pool.Length >= PoolSize;
            var bar = full ? pool[^1].Value : (int?)null;
            var barChart = full ? pool[^1].ChartId : (Guid?)null;

            var projection = await _mediator.Send(
                new ProjectPumbilityGainsQuery(request.UserId, mix, request.Pool), cancellationToken);
            var mine = (await _scores.GetBestScores(mix, request.UserId, cancellationToken))
                .Where(s => s.Score != null)
                .ToDictionary(s => s.ChartId);

            var targets = projection.ProjectedGains
                .Where(kv => charts.ContainsKey(kv.Key))
                .Select(kv => new PumbilityTarget(kv.Key,
                    projection.ExpectedScores[kv.Key],
                    kv.Value,
                    mine.TryGetValue(kv.Key, out var held) ? held.Score : null,
                    mine.TryGetValue(kv.Key, out var broken) && broken.IsBroken,
                    projection.ChartDifficulty.TryGetValue(kv.Key, out var d) ? d : null,
                    projection.Evidence.GetValueOrDefault(kv.Key)))
                .ToDictionary(t => t.ChartId);

            // In Phoenix 2, a chart the player already cleared in Phoenix 1 does not need
            // estimating — the score is on record, and repricing it is arithmetic. Those rows
            // REPLACE any peer estimate for the same chart, because a number the player has
            // actually hit beats a quantile of what other people hit.
            if (mix == MixEnum.Phoenix2)
                foreach (var carried in await CarryoverTargets(request.UserId, request.Pool, charts,
                             bar, scoring, projection, mine, cancellationToken))
                    targets[carried.ChartId] = carried;

            // One ranked list of likely gains with two sources of evidence behind it, not two
            // lists stapled together. The cut happens AFTER the merge so a chart both sources
            // named cannot spend two of the hundred slots.
            var top = targets.Values
                .OrderByDescending(t => t.Gain)
                .Take(MaxTargets)
                .ToArray();

            return new PumbilityPageRecord(mix, request.Pool, pool.Sum(p => p.Value), bar, barChart,
                pool, waiting, top);
        }

        /// <summary>
        ///     Phoenix 1 scores worth playing here, as targets. The pool AND everything ranked
        ///     behind it: capping at the fiftieth hid the rows with the best evidence there is,
        ///     because against a thin Phoenix 2 pool a repriced #73 still clears the bar
        ///     (owner, 2026-08-06). Excludes anything with no Phoenix 2 appearance (unplayable —
        ///     the panel states those as a fact).
        ///     <para>
        ///         A chart already scored here is NOT excluded. An 980k in Phoenix 1 against a
        ///         900k here is a gain worth playing for, and the question is the same one the
        ///         peer projection asks: how much does this beat what it would displace. So the
        ///         floor is the same too — your standing value on the chart, or the pool's bar,
        ///         whichever is higher — and both sources of evidence end up on one ranked list
        ///         priced identically.
        ///     </para>
        /// </summary>
        private async Task<IReadOnlyList<PumbilityTarget>> CarryoverTargets(Guid userId, ChartType? poolScope,
            IReadOnlyDictionary<Guid, Chart> charts, int? bar, ScoringConfiguration scoring,
            PumbilityProjection projection, IReadOnlyDictionary<Guid, RecordedPhoenixScore> mine,
            CancellationToken cancellationToken)
        {
            var carryover = await Handle(new ProjectPhoenix2CarryoverQuery(userId, poolScope), cancellationToken);
            var bottom = bar ?? 0;

            // What the chart is worth to the player right now. A broken run is worth nothing,
            // which is the same thing as never having played it.
            double Standing(Guid chartId)
            {
                if (!mine.TryGetValue(chartId, out var held) || held.Score == null || held.IsBroken) return 0;
                return scoring.GetScore(charts[chartId], held.Score.Value,
                    held.Plate ?? PhoenixPlate.RoughGame, held.IsBroken);
            }

            return carryover.Entries.Concat(carryover.Candidates)
                .Where(e => e.AvailableInPhoenix2 && charts.ContainsKey(e.ChartId))
                .Select(e => new
                {
                    Entry = e,
                    Gain = (int)Math.Round(e.Phoenix2Value - Math.Max(Standing(e.ChartId), bottom))
                })
                .Where(x => x.Gain > 0)
                .Select(x => new PumbilityTarget(x.Entry.ChartId,
                    // The projection IS the Phoenix 1 score. Not an estimate of it.
                    x.Entry.Phoenix1Score,
                    x.Gain,
                    mine.TryGetValue(x.Entry.ChartId, out var held) ? held.Score : null,
                    mine.TryGetValue(x.Entry.ChartId, out var broken) && broken.IsBroken,
                    projection.ChartDifficulty.TryGetValue(x.Entry.ChartId, out var d) ? d : null,
                    // No peer evidence line: nothing was estimated, so there is nothing to
                    // report about how many people were heard from.
                    null,
                    TargetSource.Phoenix1))
                .OrderByDescending(t => t.Gain)
                .Take(MaxTargets)
                .ToArray();
        }

        public async Task<Phoenix2CarryoverRecord> Handle(ProjectPhoenix2CarryoverQuery request,
            CancellationToken cancellationToken)
        {
            var phoenixCharts = (await _mediator.Send(new GetChartsQuery(MixEnum.Phoenix), cancellationToken))
                .ToDictionary(c => c.Id);
            var phoenix2Charts = (await _mediator.Send(new GetChartsQuery(MixEnum.Phoenix2), cancellationToken))
                .Select(c => c.Id)
                .ToHashSet();

            var p1Scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, false);
            var p2Scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);

            var phoenixScores = (await _scores.GetBestScores(MixEnum.Phoenix, request.UserId, cancellationToken))
                .Where(s => s is { Score: not null, IsBroken: false } && phoenixCharts.ContainsKey(s.ChartId))
                .Where(s => request.Pool == null || phoenixCharts[s.ChartId].Type == request.Pool)
                .ToArray();
            // A stage break here is not a score you hold — it rates zero, and a chart you broke
            // on is precisely the chart your Phoenix 1 record has something to say about.
            // Reading it as "already scored in Phoenix 2" is what hid those rows entirely.
            var phoenix2Scores = (await _scores.GetBestScores(MixEnum.Phoenix2, request.UserId, cancellationToken))
                .Where(s => s is { Score: not null, IsBroken: false })
                .ToDictionary(s => s.ChartId, s => s.Score!.Value);

            // The repricing: every Phoenix 1 score run through Phoenix 2's formula, which pays
            // a Singles chart one level up the base curve and zeroes anything under level 10.
            // That rule alone can turn a doubles pool into a singles pool.
            // Repriced to CandidateDepth, sliced at PoolSize. Every score was already being
            // repriced before the Take, so reading past the fiftieth costs nothing: the pool
            // figures below still come from the first fifty and mean exactly what they did.
            var ranked = phoenixScores
                .Select(s => (Score: s, Chart: phoenixCharts[s.ChartId],
                    Value: p2Scoring.GetScore(phoenixCharts[s.ChartId], s.Score!.Value,
                        s.Plate ?? PhoenixPlate.RoughGame, s.IsBroken)))
                .OrderByDescending(x => x.Value)
                .Take(CandidateDepth)
                .ToArray();
            var repriced = ranked.Take(PoolSize).ToArray();

            var phoenix1Pool = phoenixScores
                .Select(s => (Chart: phoenixCharts[s.ChartId],
                    Value: p1Scoring.GetScore(phoenixCharts[s.ChartId], s.Score!.Value,
                        s.Plate ?? PhoenixPlate.RoughGame, s.IsBroken)))
                .OrderByDescending(x => x.Value)
                .Take(PoolSize)
                .ToArray();

            CarryoverEntry Entry((RecordedPhoenixScore Score, Chart Chart, double Value) x, int index)
            {
                return new CarryoverEntry(index + 1, x.Score.ChartId, x.Score.Score!.Value,
                    x.Score.Score!.Value.LetterGradeFor(MixEnum.Phoenix),
                    Math.Round(x.Value, 2),
                    phoenix2Scores.TryGetValue(x.Score.ChartId, out var here) ? here : null,
                    phoenix2Charts.Contains(x.Score.ChartId));
            }

            var entries = repriced.Select(Entry).ToArray();
            // Place keeps counting past the pool, so a candidate can say it was your #73.
            var candidates = ranked.Skip(PoolSize)
                .Select((x, i) => Entry(x, PoolSize + i))
                .ToArray();

            return new Phoenix2CarryoverRecord(
                Math.Round(repriced.Sum(x => x.Value), 2),
                repriced.Length >= PoolSize ? Math.Round(repriced.Min(x => x.Value), 2) : 0,
                phoenix2Scores.Count,
                entries.Count(e => e.Phoenix2Score == null),
                entries.Where(e => !e.AvailableInPhoenix2).Select(e => e.ChartId).ToArray(),
                repriced.Count(x => x.Chart.Type == ChartType.Single),
                repriced.Count(x => x.Chart.Type == ChartType.Double),
                phoenix1Pool.Count(x => x.Chart.Type == ChartType.Single),
                phoenix1Pool.Count(x => x.Chart.Type == ChartType.Double),
                entries,
                candidates);
        }
    }
}
