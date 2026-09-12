using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Application
{
    /// <summary>
    ///     The players holding one title, and the viewer standing among them
    ///     (docs/design/pumbility-overhaul.md D68). This is the Breakdown card's population, and it
    ///     is deliberately not the projection's: peers are drawn to guess what you would score, on a
    ///     window around your own pool, and this card guesses nothing — it says what somebody at your
    ///     level holds, which is a question about a title.
    ///     <para>
    ///         Everything expensive here is about the band and nothing about the reader, so it is read
    ///         once per band and shared (<see cref="PumbilityCohortCache" />). What is per-viewer is
    ///         their own fifty, which is one read and no arithmetic worth keeping.
    ///     </para>
    /// </summary>
    internal sealed class PumbilityCohortSaga : IRequestHandler<GetPumbilityTitleCohortQuery, PumbilityCohortRecord>
    {
        private readonly PumbilityCohortCache _cache;
        private readonly IMediator _mediator;
        private readonly IOfficialPlacementReader _official;
        private readonly IScoreReader _scores;
        private readonly IPlayerStatsReader _stats;
        private readonly ITitleRepository _titles;

        public PumbilityCohortSaga(IMediator mediator, PumbilityCohortCache cache, IScoreReader scores,
            IPlayerStatsReader stats, ITitleRepository titles, IOfficialPlacementReader official)
        {
            _mediator = mediator;
            _cache = cache;
            _scores = scores;
            _stats = stats;
            _titles = titles;
            _official = official;
        }

        public async Task<PumbilityCohortRecord> Handle(GetPumbilityTitleCohortQuery request,
            CancellationToken cancellationToken)
        {
            var (userId, mix, pool, chosen) = request;
            var charts = (await _mediator.Send(new GetChartsQuery(mix), cancellationToken)).ToDictionary(c => c.Id);
            var scoring = ScoringConfiguration.PumbilityScoring(mix, false);

            var (band, reading) = await Cohort(mix, pool, userId, chosen, charts, scoring, cancellationToken);
            if (band is not { } name) return PumbilityCohortRecord.Empty;

            var mine = await MyPool(mix, pool, userId, charts, scoring, cancellationToken);
            // The archetypes are the merged fifty's own statement, like the split: banding a typed
            // fifty would hand the viewer an archetype their chip disagrees with, which 77% of
            // full-fifty accounts would see (§4.15).
            var archetypes = pool == PumbilityPool.Total
                ? ArchetypeSpread.Of(reading.Archetypes, reading.Spread.BoardHolders,
                    mine.Select(m => (int)m.Score).ToArray(), mix)
                : null;
            return new PumbilityCohortRecord(name, reading.Spread.Holders, reading.Spread.BoardHolders,
                PeerLevelSpread.Of(reading.Spread, charts, mine.Select(m => m.ChartId)), reading.Split,
                reading.BoardAsOf, archetypes);
        }

        /// <summary>
        ///     The band to answer for, and what standing on it looks like. A viewer who picked a band
        ///     gets exactly that one; otherwise it is their own, and on the merged ladder their own
        ///     level gives way to the gem around it when too few players stand on it
        ///     (<see cref="PumbilityBand.MinimumForLevel" />, measured in §4.14).
        ///     <para>
        ///         The thin level is read rather than counted, because the read of a level too thin to
        ///         keep is by definition a read of under twenty-five players — cheaper than the extra
        ///         query it would take to avoid it, and cached either way.
        ///     </para>
        /// </summary>
        private async Task<(Name? Band, CohortReading Reading)> Cohort(MixEnum mix, PumbilityPool pool, Guid userId,
            Name? chosen, IReadOnlyDictionary<Guid, Chart> charts, ScoringConfiguration scoring,
            CancellationToken cancellationToken)
        {
            // Phoenix 1 has no ladder to band a pool on: a difficulty title is rating earned on one
            // level, which no total can be banded into, and the highest one is already stored.
            if (mix != MixEnum.Phoenix2)
            {
                var mine = chosen ?? await _titles.GetHighestTitle(mix, userId, cancellationToken);
                if (mine is not { } title) return (null, Empty);
                return (title, await Read(mix, pool, null, title, charts, scoring));
            }

            if (chosen is { } picked)
            {
                var band = PumbilityBand.ByName(pool, picked);
                if (band == null) return (null, Empty);
                return (band.Name, await Read(mix, pool, band, band.Name, charts, scoring));
            }

            var stats = await _stats.GetStats(mix, userId, cancellationToken);
            var value = pool switch
            {
                PumbilityPool.Singles => stats.SinglesRating,
                PumbilityPool.Doubles => stats.DoublesRating,
                _ => stats.SkillRating
            };

            var level = PumbilityBand.LevelOf(pool, value);
            if (level == null) return (null, Empty);
            var reading = await Read(mix, pool, level, level.Name, charts, scoring);

            // A typed rung is the cohort whatever its size — there is nothing coarser inside a [S] or
            // [D] ladder to fall back to, so a rung of six reads as a rung of six.
            if (reading.Spread.Holders >= PumbilityBand.MinimumForLevel || level.Gem is not { } gem)
                return (level.Name, reading);

            var around = PumbilityBand.ByName(pool, gem);
            if (around == null) return (level.Name, reading);
            return (around.Name, await Read(mix, pool, around, around.Name, charts, scoring));
        }

        private static CohortReading Empty => new(CohortLevelSpread.Empty, CohortArchetypeSpread.Empty, null, null);

        /// <summary>
        ///     Everyone standing on one band and what their fifties are made of, through the cache
        ///     that makes it one read per band rather than one per reader. Site players and board
        ///     players fold together: a board row that resolves to an account is left out, because
        ///     that person was already answered by the ladder's own read (D61).
        /// </summary>
        private Task<CohortReading> Read(MixEnum mix, PumbilityPool pool, PumbilityBand? band, Name title,
            IReadOnlyDictionary<Guid, Chart> charts, ScoringConfiguration scoring)
        {
            return _cache.GetOrAdd(mix, pool, title, () => Sweep(mix, pool, band, title, charts, scoring));
        }

        private async Task<CohortReading> Sweep(MixEnum mix, PumbilityPool pool, PumbilityBand? band, Name title,
            IReadOnlyDictionary<Guid, Chart> charts, ScoringConfiguration scoring)
        {
            // No request token: a band's reading outlives the request that first asked for it, and
            // cancelling it would fault the cached task for everyone else waiting on the same one.
            var token = CancellationToken.None;
            var types = TypesOf(pool);
            var holders = band == null
                ? (await _titles.GetUserIdsWithHighestTitle(mix, title, token)).ToHashSet()
                : (await _stats.GetPlayersInPoolBand(mix, pool, band.Floor, band.Ceiling, token)).ToHashSet();

            var records = new List<UserPhoenixScore>();
            foreach (var type in types)
                records.AddRange(await _scores.GetPlayerScoresInLevelRange(mix, holders, type,
                    PeerGroup.PumbilityPoolFloor, DifficultyLevel.Max, token));

            var (boardScores, boardTotals, boardAsOf) = band == null
                ? (Array.Empty<BoardPeerScore>(), new Dictionary<PeerVoice, double>(), (DateTimeOffset?)null)
                : await BoardHolders(mix, pool, band, types, token);

            var voices = holders.Select(PeerVoice.Account).Concat(boardTotals.Keys).ToHashSet();
            var summary = PumbilityPeerPools.Build(records, voices, charts, scoring, boardScores, boardTotals);

            // The split is the merged fifty's own statement, so it belongs to the merged pool alone —
            // a singles or doubles pool is one type by definition.
            var split = pool == PumbilityPool.Total
                ? PumbilityPoolSplit.Average(Priced(records, boardScores, charts, scoring).Values)
                : null;
            return new CohortReading(CohortLevelSpread.Of(summary, charts),
                CohortArchetypeSpread.Of(summary, mix), split, boardAsOf);
        }

        /// <summary>
        ///     The board's own players on the band and what they published, over the types the pool
        ///     holds — the players no account claims, since the ladder's own read already answered
        ///     for everybody who has one and counting both would count them twice (D61).
        /// </summary>
        private async Task<(BoardPeerScore[] Scores, Dictionary<PeerVoice, double> Totals, DateTimeOffset? AsOf)>
            BoardHolders(MixEnum mix, PumbilityPool pool, PumbilityBand band, IReadOnlyList<ChartType> types,
                CancellationToken token)
        {
            // Nobody is excluded by who is asking, and nobody an account claims comes back at all —
            // the mirror answers both, which is what lets one read serve every reader of the band.
            var reading = await _official.GetBoardBand(mix, pool, band.Floor, band.Ceiling, token);
            var onBoard = reading?.Peers ?? Array.Empty<BoardPeerReading>();
            var voiceOf = new Dictionary<int, PeerVoice>();
            var totals = new Dictionary<PeerVoice, double>();
            foreach (var player in onBoard)
            {
                var voice = PeerVoice.FromBoard(player.BoardPlayerIds[0], player.Tag);
                totals[voice] = player.Pool;
                foreach (var id in player.BoardPlayerIds) voiceOf[id] = voice;
            }

            if (voiceOf.Count == 0) return (Array.Empty<BoardPeerScore>(), totals, null);

            var rows = new List<BoardScoreReading>();
            foreach (var type in types)
                rows.AddRange(await _official.GetBoardScores(mix, type, voiceOf.Keys.ToArray(),
                    PeerGroup.PumbilityPoolFloor, DifficultyLevel.Max, token));

            // One row per person per chart: a player who owns two board rows votes once (D61).
            var scores = rows.Where(r => voiceOf.ContainsKey(r.BoardPlayerId))
                .GroupBy(r => (voiceOf[r.BoardPlayerId], r.ChartId))
                .Select(g => new BoardPeerScore(g.Key.Item1, g.Key.ChartId, g.Max(r => r.Score)))
                .ToArray();
            return (scores, totals, reading?.AsOf);
        }

        /// <summary>
        ///     Every holder's records priced for the split, board players among them — the same rows
        ///     the pools were folded from, so the two halves of the card cannot disagree about who
        ///     holds what.
        /// </summary>
        private static Dictionary<PeerVoice, List<PricedRecord>> Priced(IEnumerable<UserPhoenixScore> records,
            IEnumerable<BoardPeerScore> boardScores, IReadOnlyDictionary<Guid, Chart> charts,
            ScoringConfiguration scoring)
        {
            var byHolder = new Dictionary<PeerVoice, List<PricedRecord>>();

            void Add(PeerVoice voice, Chart chart, PhoenixScore score, PhoenixPlate plate, bool isBroken)
            {
                if (!byHolder.TryGetValue(voice, out var list)) byHolder[voice] = list = new List<PricedRecord>();
                list.Add(new PricedRecord(chart.Type, scoring.GetScore(chart, score, plate, isBroken)));
            }

            foreach (var record in records)
                if (charts.TryGetValue(record.ChartId, out var chart))
                    Add(PeerVoice.Account(record.UserId), chart, record.Score, record.Plate ?? PhoenixPlate.RoughGame,
                        record.IsBroken);

            // A board row carries a score and no plate, priced at the plate that score most plausibly
            // carries — the same expectation the pools were built with.
            foreach (var row in boardScores)
                if (charts.TryGetValue(row.ChartId, out var chart))
                    Add(row.Voice, chart, PhoenixScore.From(row.Score),
                        ScoringConfiguration.ExpectedPlateForScore(PhoenixScore.From(row.Score)), false);

            return byHolder;
        }

        /// <summary>
        ///     The viewer's own fifty of the pool, by the same rule the cohort's were built with: the
        ///     fifty highest-priced non-broken records above zero, over the types the pool holds.
        /// </summary>
        private async Task<IReadOnlyList<(Guid ChartId, PhoenixScore Score)>> MyPool(MixEnum mix,
            PumbilityPool pool, Guid userId, IReadOnlyDictionary<Guid, Chart> charts, ScoringConfiguration scoring,
            CancellationToken cancellationToken)
        {
            var types = TypesOf(pool);
            // One definition of the viewer's fifty, carried out with its scores as well as its
            // charts: the level spread counts the charts and the archetype bands the scores, and
            // the two describing different fifties is exactly the disagreement to avoid.
            return (await _scores.GetBestScores(mix, userId, cancellationToken))
                .Where(r => r.Score != null && !r.IsBroken && charts.ContainsKey(r.ChartId)
                            && types.Contains(charts[r.ChartId].Type))
                .Select(r => (r.ChartId, Score: r.Score!.Value, Rating: scoring.GetScore(charts[r.ChartId],
                    r.Score!.Value, r.Plate ?? PhoenixPlate.RoughGame, r.IsBroken)))
                .Where(r => r.Rating > 0)
                .OrderByDescending(r => r.Rating).ThenBy(r => r.ChartId)
                .Take(PumbilityPeerPools.PoolSize)
                .Select(r => (r.ChartId, r.Score))
                .ToArray();
        }

        /// <summary>
        ///     The chart types a pool is made of. The merged pool holds both, and its fifty is one
        ///     fifty drawn across them — never a singles fifty added to a doubles one.
        /// </summary>
        private static IReadOnlyList<ChartType> TypesOf(PumbilityPool pool)
        {
            return pool switch
            {
                PumbilityPool.Singles => new[] { ChartType.Single },
                PumbilityPool.Doubles => new[] { ChartType.Double },
                _ => new[] { ChartType.Single, ChartType.Double }
            };
        }
    }
}
