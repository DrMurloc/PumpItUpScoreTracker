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
                             bar, projection, mine, cancellationToken))
                    targets[carried.ChartId] = carried;

            return new PumbilityPageRecord(mix, request.Pool, pool.Sum(p => p.Value), bar, barChart,
                pool, waiting, targets.Values.OrderByDescending(t => t.Gain).ToArray());
        }

        /// <summary>
        ///     Phoenix 1 scores that would land in the requested Phoenix 2 pool, as targets.
        ///     Excludes anything already scored here (done) and anything with no Phoenix 2
        ///     appearance (unplayable — the carryover panel states those as a fact instead).
        /// </summary>
        private async Task<IReadOnlyList<PumbilityTarget>> CarryoverTargets(Guid userId, ChartType? poolScope,
            IReadOnlyDictionary<Guid, Chart> charts, int? bar, PumbilityProjection projection,
            IReadOnlyDictionary<Guid, RecordedPhoenixScore> mine, CancellationToken cancellationToken)
        {
            var carryover = await Handle(new ProjectPhoenix2CarryoverQuery(userId, poolScope), cancellationToken);
            var floor = bar ?? 0;

            return carryover.Entries
                .Where(e => e.Phoenix2Score == null && e.AvailableInPhoenix2 && charts.ContainsKey(e.ChartId))
                .Select(e => new
                {
                    Entry = e,
                    Gain = (int)Math.Round(e.Phoenix2Value - floor)
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
            var phoenix2Scores = (await _scores.GetBestScores(MixEnum.Phoenix2, request.UserId, cancellationToken))
                .Where(s => s.Score != null)
                .ToDictionary(s => s.ChartId, s => s.Score!.Value);

            // The repricing: every Phoenix 1 score run through Phoenix 2's formula, which pays
            // a Singles chart one level up the base curve and zeroes anything under level 10.
            // That rule alone can turn a doubles pool into a singles pool.
            var repriced = phoenixScores
                .Select(s => (Score: s, Chart: phoenixCharts[s.ChartId],
                    Value: p2Scoring.GetScore(phoenixCharts[s.ChartId], s.Score!.Value,
                        s.Plate ?? PhoenixPlate.RoughGame, s.IsBroken)))
                .OrderByDescending(x => x.Value)
                .Take(PoolSize)
                .ToArray();

            var phoenix1Pool = phoenixScores
                .Select(s => (Chart: phoenixCharts[s.ChartId],
                    Value: p1Scoring.GetScore(phoenixCharts[s.ChartId], s.Score!.Value,
                        s.Plate ?? PhoenixPlate.RoughGame, s.IsBroken)))
                .OrderByDescending(x => x.Value)
                .Take(PoolSize)
                .ToArray();

            var entries = repriced
                .Select((x, i) => new CarryoverEntry(i + 1, x.Score.ChartId, x.Score.Score!.Value,
                    x.Score.Score!.Value.LetterGradeFor(MixEnum.Phoenix),
                    Math.Round(x.Value, 2),
                    phoenix2Scores.TryGetValue(x.Score.ChartId, out var here) ? here : null,
                    phoenix2Charts.Contains(x.Score.ChartId)))
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
                entries);
        }
    }
}
