using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
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
                .Select(s => (Score: s, Value: scoring.GetScore(charts[s.ChartId], s.Score!.Value,
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
            var bar = full ? pool[^1].Value : (double?)null;
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
                    projection.ChartDifficulty.TryGetValue(kv.Key, out var d) ? d : null))
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

            // What each chart would be worth at its best available reading — held score, or the
            // projection if that beats it. MERGED rather than summed: a chart already in the pool
            // keeps the better of the two, so nothing counts twice and no gain has to be added to
            // another one, which §8.3 forbids.
            var reachable = new Dictionary<Guid, double>();
            foreach (var (chartId, score) in mine)
                if (charts.ContainsKey(chartId))
                    reachable[chartId] = scoring.GetScore(charts[chartId], score.Score!.Value,
                        score.Plate ?? PhoenixPlate.RoughGame, score.IsBroken);
            foreach (var target in targets.Values)
            {
                var plate = mine.TryGetValue(target.ChartId, out var held)
                    ? held.Plate ?? PhoenixPlate.RoughGame
                    : PhoenixPlate.RoughGame;
                var projected = scoring.GetScore(charts[target.ChartId], target.Projected, plate, false);
                if (projected > reachable.GetValueOrDefault(target.ChartId))
                    reachable[target.ChartId] = projected;
            }

            var total = pool.Sum(p => p.Value);
            var totals = await PoolTotalsFor(request.UserId, mix, request.Pool, total, bar, charts, scoring,
                reachable, cancellationToken);

            return new PumbilityPageRecord(mix, request.Pool, total, bar, barChart,
                pool, waiting, top, Breakdown(pool, charts, scoring), totals?.Totals,
                totals?.Rails ?? Array.Empty<TitleRail>());
        }

        /// <summary>
        ///     Where a repriced Phoenix 1 record would land on each of the three ladders. Always
        ///     all three regardless of the requested scope: the panel's argument is what the whole
        ///     record is worth, and at a launch that is the only place a player sees a gem beside
        ///     their name at all.
        ///     <para>
        ///         ⚠ These are not titles held. The rails on Your Pool say what is, and at a
        ///         launch the two disagree loudly about the same three ladders — which is the
        ///         point, and why the wording on this side is always conditional (§8.2).
        ///     </para>
        /// </summary>
        private static IReadOnlyList<ProjectedTitle> ProjectedTitles(
            IReadOnlyList<RecordedPhoenixScore> everyScore, Func<Guid, Chart> priced,
            ScoringConfiguration p2Scoring)
        {
            var repriced = everyScore
                .Select(s => (Chart: priced(s.ChartId),
                    Value: p2Scoring.GetScore(priced(s.ChartId), s.Score!.Value,
                        s.Plate ?? PhoenixPlate.RoughGame, s.IsBroken)))
                .Where(x => x.Value > 0)
                .ToArray();

            var ladders = Phoenix2TitleList.BuildList().OfType<Phoenix2PumbilityTitle>()
                .GroupBy(t => t.Pool)
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.CompletionRequired).ToArray());

            return new[]
                {
                    (PumbilityPool.Total, (ChartType?)null),
                    (PumbilityPool.Singles, ChartType.Single),
                    (PumbilityPool.Doubles, ChartType.Double)
                }
                .Select(x =>
                {
                    var value = repriced
                        .Where(r => x.Item2 == null || r.Chart.Type == x.Item2)
                        .OrderByDescending(r => r.Value)
                        .Take(PoolSize)
                        .Sum(r => r.Value);

                    var ladder = ladders.TryGetValue(x.Item1, out var rungs) ? rungs : Array.Empty<Phoenix2PumbilityTitle>();
                    var held = ladder.LastOrDefault(t => t.CompletionRequired <= value);
                    var next = ladder.FirstOrDefault(t => t.CompletionRequired > value);

                    return new ProjectedTitle(x.Item1, value, held?.Name.ToString(), next?.CompletionRequired);
                })
                .ToArray();
        }

        /// <summary>
        ///     All three Phoenix 2 pools and their title ladders. The scope the caller asked for
        ///     is already computed, so only the other two are read — which is why this belongs
        ///     here rather than on the page, where filling the selector cost two more runs of
        ///     this whole handler.
        /// </summary>
        private async Task<(PoolTotals Totals, IReadOnlyList<TitleRail> Rails)?> PoolTotalsFor(Guid userId,
            MixEnum mix, ChartType? scope, double scopeTotal, double? scopeBar,
            IReadOnlyDictionary<Guid, Chart> charts, ScoringConfiguration scoring,
            IReadOnlyDictionary<Guid, double> reachable, CancellationToken cancellationToken)
        {
            // Phoenix 1 has one pool and no PUMBILITY-threshold titles, so there is neither a
            // split to show nor a ladder to show it against.
            if (mix != MixEnum.Phoenix2) return null;

            async Task<(double Total, double? Bar)> PoolFor(ChartType? type)
            {
                if (type == scope) return (scopeTotal, scopeBar);

                var values = (await _mediator.Send(new GetTop50ForPlayerQuery(userId, type, PoolSize, mix),
                        cancellationToken))
                    .Where(s => s.Score != null && charts.ContainsKey(s.ChartId))
                    .Select(s => scoring.GetScore(charts[s.ChartId], s.Score!.Value,
                        s.Plate ?? PhoenixPlate.RoughGame, s.IsBroken))
                    .OrderByDescending(v => v)
                    .ToArray();

                return (values.Sum(), values.Length >= PoolSize ? values[^1] : null);
            }

            var all = await PoolFor(null);
            var singles = await PoolFor(ChartType.Single);
            var doubles = await PoolFor(ChartType.Double);

            var ladders = Phoenix2TitleList.BuildList().OfType<Phoenix2PumbilityTitle>()
                .GroupBy(t => t.Pool)
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.CompletionRequired).ToArray());

            // ONE rail, following the pool selector (owner, 2026-08-08). The selector already
            // re-ranks the total, the bar, the curve, the board and the targets — a control that
            // moves everything in the section except this reads as broken. All three ladders
            // still exist; the other two are one click away, and the totals beside them say so.
            var rail = scope switch
            {
                ChartType.Single => Rail(PumbilityPool.Singles, singles, ChartType.Single, ChartType.Single),
                ChartType.Double => Rail(PumbilityPool.Doubles, doubles, ChartType.Double, ChartType.Double),
                // The merged ladder can be filled from either side, and Phoenix 2 pays a Singles
                // chart one level up — so the cheapest route to its ask is a single.
                _ => Rail(PumbilityPool.Total, all, ChartType.Single, null)
            };

            return (new PoolTotals(all.Total, singles.Total, doubles.Total),
                rail == null ? Array.Empty<TitleRail>() : new[] { rail });

            TitleRail? Rail(PumbilityPool pool, (double Total, double? Bar) figures, ChartType exampleType,
                ChartType? exampleScope)
            {
                if (!ladders.TryGetValue(pool, out var ladder) || ladder.Length == 0) return null;

                var held = ladder.LastOrDefault(t => t.CompletionRequired <= figures.Total);
                var next = ladder.FirstOrDefault(t => t.CompletionRequired > figures.Total);

                // A pool is fifty charts, so a threshold IS a per-chart value. That is the whole
                // device: it is true however the player gets there, where a count of charts
                // would have to assume an order the gain column deliberately does not have.
                var ask = next == null ? 0 : next.CompletionRequired / (double)PoolSize;
                var examples = next == null
                    ? Array.Empty<AskExample>()
                    : AskExamples(scoring, exampleType, ask);

                // What the fifty would average if every suggestion landed. Over PoolSize rather
                // than over however many charts exist, so it is comparable to the ask and to the
                // average beside it.
                var projectable = reachable
                    .Where(kv => exampleScope == null || charts[kv.Key].Type == exampleScope)
                    .Select(kv => kv.Value)
                    .Where(v => v > 0)
                    .OrderByDescending(v => v)
                    .Take(PoolSize)
                    .ToArray();

                return new TitleRail(pool, figures.Total, held?.Name.ToString(),
                    held?.CompletionRequired ?? 0, next?.Name.ToString(), next?.CompletionRequired,
                    ask, figures.Total / (double)PoolSize, figures.Bar, examples,
                    projectable.Length == 0 ? null : projectable.Sum() / (double)PoolSize);
            }
        }

        /// <summary>
        ///     A grade multiplier this site has actually verified. B and below are the unverified
        ///     −0.05 extrapolation in the Phoenix 2 config, so A is the floor: anchoring lower
        ///     would print a level derived from a guess.
        /// </summary>
        private static readonly PhoenixLetterGrade[] ReferenceGrades =
            { PhoenixLetterGrade.SSSPlus, PhoenixLetterGrade.AAA, PhoenixLetterGrade.A };

        /// <summary>
        ///     What chart meets a per-chart ask, at each reference grade. One reference was the
        ///     wrong shape — play quality moves the answer by several levels, so a single number
        ///     is right only for the player already performing at it.
        ///     <para>
        ///         Best grade first, which makes the levels ascend downward: the low level is the
        ///         hard one. A grade that cannot reach the ask at any level is omitted rather than
        ///         reported against the ceiling, because naming a ceiling that falls short would
        ///         read as an answer.
        ///     </para>
        /// </summary>
        private static AskExample[] AskExamples(ScoringConfiguration scoring, ChartType type, double ask)
        {
            if (ask <= 0) return Array.Empty<AskExample>();

            return ReferenceGrades
                .Select(grade =>
                {
                    var score = grade.GetMinimumScoreFor(scoring.Mix);
                    var level = DifficultyLevel.All
                        .OrderBy(l => (int)l)
                        .Cast<DifficultyLevel?>()
                        .FirstOrDefault(l => scoring.GetScore(type, l!.Value, score,
                            PhoenixPlate.RoughGame) >= ask);
                    return level == null ? null : new AskExample(grade, level.Value, type);
                })
                .Where(e => e != null)
                .Select(e => e!)
                .ToArray();
        }

        /// <summary>
        ///     Where the pool's total comes from, and what a perfect plate on all fifty would add.
        ///     Both are the scoring configuration's own arithmetic — the page must never carry a
        ///     second opinion about a formula that still has unverified values in it.
        /// </summary>
        private static PoolBreakdown Breakdown(IReadOnlyList<PoolEntry> pool,
            IReadOnlyDictionary<Guid, Chart> charts, ScoringConfiguration scoring)
        {
            var split = default(ScoreContribution);
            var headroom = 0d;
            foreach (var entry in pool)
            {
                var chart = charts[entry.ChartId];
                var plate = entry.Plate ?? PhoenixPlate.RoughGame;
                split += scoring.Decompose(chart, entry.Score, plate, entry.IsBroken);
                headroom += scoring.PlateHeadroom(chart, entry.Score, plate, entry.IsBroken);
            }

            return new PoolBreakdown(Math.Round(split.Base, 2), Math.Round(split.FromGrade, 2),
                Math.Round(split.FromPlate, 2), Math.Round(headroom, 2));
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
            IReadOnlyDictionary<Guid, Chart> charts, double? bar, ScoringConfiguration scoring,
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
                    Gain = e.Phoenix2Value - Math.Max(Standing(e.ChartId), bottom)
                })
                .Where(x => x.Gain > 0)
                .Select(x => new PumbilityTarget(x.Entry.ChartId,
                    // The projection IS the Phoenix 1 score. Not an estimate of it.
                    x.Entry.Phoenix1Score,
                    x.Gain,
                    mine.TryGetValue(x.Entry.ChartId, out var held) ? held.Score : null,
                    mine.TryGetValue(x.Entry.ChartId, out var broken) && broken.IsBroken,
                    projection.ChartDifficulty.TryGetValue(x.Entry.ChartId, out var d) ? d : null,
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
                .ToDictionary(c => c.Id);

            var p1Scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, false);
            var p2Scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);

            // Phoenix 2 RERATED the charts it inherited rather than restepping them, so one
            // chart id carries a different level in each mix. The repricing has to read the
            // level the chart carries HERE: priced from Phoenix 1, a downrated chart pays a
            // base it no longer commands and an uprated one is short-changed by the same
            // arithmetic. A chart with no Phoenix 2 row has no Phoenix 2 level to read, so it
            // keeps its own — it still counts toward the pool, and it can never be a target.
            Chart Priced(Guid chartId)
            {
                return phoenix2Charts.TryGetValue(chartId, out var here) ? here : phoenixCharts[chartId];
            }

            var everyPhoenixScore =
                (await _scores.GetBestScores(MixEnum.Phoenix, request.UserId, cancellationToken))
                .Where(s => s is { Score: not null, IsBroken: false } && phoenixCharts.ContainsKey(s.ChartId))
                .ToArray();
            var phoenixScores = everyPhoenixScore
                .Where(s => request.Pool == null || phoenixCharts[s.ChartId].Type == request.Pool)
                .ToArray();
            // A stage break here is not a score you hold — it rates zero, and a chart you broke
            // on is precisely the chart your Phoenix 1 record has something to say about.
            // Reading it as "already scored in Phoenix 2" is what hid those rows entirely.
            var phoenix2Scores = (await _scores.GetBestScores(MixEnum.Phoenix2, request.UserId, cancellationToken))
                .Where(s => s is { Score: not null, IsBroken: false })
                .ToDictionary(s => s.ChartId, s => s.Score!.Value);

            // The repricing: every Phoenix 1 score run through Phoenix 2's formula at the level
            // the chart carries in Phoenix 2, which pays a Singles chart one level up the base
            // curve and zeroes anything under level 10. The singles bump alone can turn a
            // doubles pool into a singles pool.
            // Repriced to CandidateDepth, sliced at PoolSize. Every score was already being
            // repriced before the Take, so reading past the fiftieth costs nothing: the pool
            // figures below still come from the first fifty and mean exactly what they did.
            // Worth nothing, worth no slot — the same rule GetTop50ForPlayerQuery applies to a
            // live pool. Here it bites on the two mixes' different floors: a level-9 chart pays
            // in neither, and a level-10 through -14 chart that pays in Phoenix 1 prices at zero
            // under Phoenix 2's sub-10 rule the moment its singles bump is not enough. Without
            // this an account with under fifty counting charts fills the rest of its repriced
            // fifty with zeros, which drives the bar this pool would set to zero and miscounts
            // the singles/doubles split the panel is built to show.
            var ranked = phoenixScores
                .Select(s => (Score: s, Chart: Priced(s.ChartId),
                    Value: p2Scoring.GetScore(Priced(s.ChartId), s.Score!.Value,
                        s.Plate ?? PhoenixPlate.RoughGame, s.IsBroken)))
                .Where(x => x.Value > 0)
                .OrderByDescending(x => x.Value)
                .Take(CandidateDepth)
                .ToArray();
            var repriced = ranked.Take(PoolSize).ToArray();

            var phoenix1Pool = phoenixScores
                .Select(s => (Chart: phoenixCharts[s.ChartId],
                    Value: p1Scoring.GetScore(phoenixCharts[s.ChartId], s.Score!.Value,
                        s.Plate ?? PhoenixPlate.RoughGame, s.IsBroken)))
                .Where(x => x.Value > 0)
                .OrderByDescending(x => x.Value)
                .Take(PoolSize)
                .ToArray();

            CarryoverEntry Entry((RecordedPhoenixScore Score, Chart Chart, double Value) x, int index)
            {
                return new CarryoverEntry(index + 1, x.Score.ChartId, x.Score.Score!.Value,
                    x.Score.Score!.Value.LetterGradeFor(MixEnum.Phoenix),
                    x.Value,
                    phoenix2Scores.TryGetValue(x.Score.ChartId, out var here) ? here : null,
                    phoenix2Charts.ContainsKey(x.Score.ChartId));
            }

            var entries = repriced.Select(Entry).ToArray();
            // Place keeps counting past the pool, so a candidate can say it was your #73.
            var candidates = ranked.Skip(PoolSize)
                .Select((x, i) => Entry(x, PoolSize + i))
                .ToArray();

            return new Phoenix2CarryoverRecord(
                repriced.Sum(x => x.Value),
                repriced.Length >= PoolSize ? repriced.Min(x => x.Value) : 0,
                phoenix2Scores.Count,
                entries.Count(e => e.Phoenix2Score == null),
                ProjectedTitles(everyPhoenixScore, Priced, p2Scoring),
                repriced.Count(x => x.Chart.Type == ChartType.Single),
                repriced.Count(x => x.Chart.Type == ChartType.Double),
                phoenix1Pool.Count(x => x.Chart.Type == ChartType.Single),
                phoenix1Pool.Count(x => x.Chart.Type == ChartType.Double),
                entries,
                candidates);
        }
    }
}
