using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Application;

/// <summary>
///     The site half of Hardmode (docs/design/hardmode-leaderboard.md §3): every account's three
///     pool totals, written once a week off the census, and the viewer's own page read at request
///     time.
///     <para>
///         The totals are stored because a leaderboard has to be an ordered read. The viewer's
///         own pool is not: it is one indexed read of their records against a list of chart ids,
///         which keeps their number current with their last import while the list itself holds
///         until Sunday.
///     </para>
/// </summary>
internal sealed class HardmodeSaga :
    IConsumer<HardmodeChartsRebuiltEvent>,
    IRequestHandler<GetHardmodePageQuery, HardmodePageRecord>,
    IRequestHandler<GetHardmodeBoardQuery, HardmodeBoardRecord>,
    IRequestHandler<HardmodeSaga.RepriceHardmodePool, HardmodeSaga.HardmodeReprice>
{
    /// <summary>
    ///     Reprice ONE account against the current chart list. The list is weekly so the board
    ///     does not jerk about day to day, but the standing on it is not: it moves with the
    ///     player's last import, the way every other PUMBILITY number does (owner, 2026-09-12).
    ///     <para>
    ///         An in-process step of the score-batch pipeline, like the rating and title steps,
    ///         rather than a bus message of its own — ordering comes from pipeline shape.
    ///     </para>
    ///     <para>
    ///         <paramref name="Changes" /> is the batch that triggered it, and is what lets the
    ///         step report a per-chart gain rather than only a total; null (the weekly sweep, a
    ///         backfill) reprices and reports the totals with no attribution.
    ///     </para>
    /// </summary>
    public sealed record RepriceHardmodePool(Guid UserId, MixEnum Mix,
        IReadOnlyList<PlayerScoresUpdatedEvent.ScoreChange>? Changes = null) : IRequest<HardmodeReprice>;

    /// <summary>
    ///     One pool across the batch. <paramref name="Rank" /> and <paramref name="Field" /> are
    ///     the account's standing on that pool's board afterwards — null when it holds nothing
    ///     there, which is the same rule the page's own standing applies.
    /// </summary>
    public sealed record HardmodePoolMove(double Old, double New, int Held, int? Rank, int? Field)
    {
        public static HardmodePoolMove None { get; } = new(0, 0, 0, null, null);

        public bool Gained => New > Old;
    }

    /// <summary>
    ///     What the Hardmode step learned, for the capture saga to turn into milestones, flags and
    ///     detail. Carries the qualifying set it already read so capture does not read it again,
    ///     and the combined-pool ranks and gains so the score badge and its chip cost no further
    ///     work (docs/design/hardmode-leaderboard.md §10).
    /// </summary>
    public sealed record HardmodeReprice(
        HardmodePoolMove Combined,
        HardmodePoolMove Singles,
        HardmodePoolMove Doubles,
        IReadOnlySet<Guid> Qualifying,
        IReadOnlyDictionary<Guid, int> Ranks,
        IReadOnlyDictionary<Guid, double> Gains,
        bool Persisted = false)
    {
        /// <summary>A mix with no census — every consumer reads this as "Hardmode is not live here".</summary>
        public static HardmodeReprice None { get; } = new(HardmodePoolMove.None, HardmodePoolMove.None,
            HardmodePoolMove.None, new HashSet<Guid>(), new Dictionary<Guid, int>(),
            new Dictionary<Guid, double>());
    }

    /// <summary>A pool is fifty, which is what makes a threshold a per-chart ask.</summary>
    private const int PoolSize = 50;

    private readonly IChartRepository _charts;
    private readonly IHardmodeChartReader _hardmodeCharts;
    private readonly ILogger<HardmodeSaga> _logger;
    private readonly IHardmodeRatingRepository _ratings;
    private readonly IScoreReader _scores;

    public HardmodeSaga(IHardmodeRatingRepository ratings, IHardmodeChartReader hardmodeCharts,
        IScoreReader scores, IChartRepository charts, ILogger<HardmodeSaga> logger)
    {
        _ratings = ratings;
        _hardmodeCharts = hardmodeCharts;
        _scores = scores;
        _charts = charts;
        _logger = logger;
    }

    /// <summary>
    ///     Reprice every account against the new list. The mix is zeroed first: a player who lost
    ///     a score, or a chart that stopped qualifying, would otherwise keep last week's total
    ///     forever — and on a board rebuilt weekly that is the one failure nobody would notice.
    /// </summary>
    public async Task Consume(ConsumeContext<HardmodeChartsRebuiltEvent> context)
    {
        var mix = context.Message.Mix;
        var cancellationToken = context.CancellationToken;
        var qualifying = (await _hardmodeCharts.GetQualifyingCharts(mix, cancellationToken))
            .Select(c => c.ChartId).ToHashSet();
        if (qualifying.Count == 0)
        {
            _logger.LogInformation("Hardmode ratings skipped for {Mix}: the list is empty", mix);
            return;
        }

        var charts = (await _charts.GetCharts(mix, cancellationToken: cancellationToken))
            .Where(c => qualifying.Contains(c.Id))
            .ToDictionary(c => c.Id);
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);
        var valued = new Dictionary<Guid, List<(ChartType Type, double Value)>>();

        foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
        foreach (var level in DifficultyLevel.All)
        foreach (var (userId, record) in await _scores.GetScores(mix, chartType, level, cancellationToken))
        {
            if (record.Score == null || record.IsBroken) continue;
            if (!charts.TryGetValue(record.ChartId, out var chart)) continue;
            var value = scoring.GetScore(chart, record.Score.Value,
                record.Plate ?? ScoringConfiguration.ExpectedPlateForScore(record.Score.Value), false);
            if (value <= 0) continue;
            (valued.TryGetValue(userId, out var list) ? list : valued[userId] = new()).Add((chart.Type, value));
        }

        await _ratings.Clear(mix, cancellationToken);
        var rows = valued.Select(kv => Row(kv.Key, kv.Value)).ToArray();
        await _ratings.Save(mix, rows, cancellationToken);
        _logger.LogInformation("Hardmode ratings for {Mix}: {Accounts} accounts over {Charts} charts", mix,
            rows.Length, qualifying.Count);
    }

    public async Task<HardmodeReprice> Handle(RepriceHardmodePool request,
        CancellationToken cancellationToken)
    {
        var qualifying = await _hardmodeCharts.GetQualifyingCharts(request.Mix, cancellationToken);
        if (qualifying.Count == 0) return HardmodeReprice.None;

        // Read before write, because the announcement is the DIFFERENCE and Save overwrites it.
        //
        // ⚠ A NULL row is not "zeroes". Save only touches accounts that already have a
        // PlayerStats row, so a null here means the write below is going to skip this account
        // entirely — and announcing "0 → N" against a number nothing persisted would mint the
        // identical milestone again on the next import, and the one after. An existing row with
        // zeroed Hardmode columns is the real first-ever case and comes back as a row.
        var before = await _ratings.Get(request.Mix, request.UserId, cancellationToken);
        var priced = await Priced(request.Mix, request.UserId, qualifying, cancellationToken);
        var after = Row(request.UserId, priced.Select(p => (p.Chart.Type, p.Value)).ToArray());

        // Written even when it prices to nothing: a player who lost their only qualifying score
        // has to stop carrying last week's number, and Save touches this account alone.
        await _ratings.Save(request.Mix, new[] { after }, cancellationToken);

        var combined = await Move(request, before?.Combined ?? 0, after.Combined, after.Held, null,
            cancellationToken);
        var singles = await Move(request, before?.Singles ?? 0, after.Singles, after.SinglesHeld,
            ChartType.Single, cancellationToken);
        var doubles = await Move(request, before?.Doubles ?? 0, after.Doubles, after.DoublesHeld,
            ChartType.Double, cancellationToken);

        // Rank and gain both speak for the COMBINED pool, the way PumbilityRank and PumbilityGain
        // do: the score row reports one number, and the per-type pools are the page's business.
        var fifty = Fifty(priced, null);
        var ranks = fifty.Select((p, i) => (p.Record.ChartId, Rank: i + 1))
            .ToDictionary(x => x.ChartId, x => x.Rank);

        return new HardmodeReprice(combined, singles, doubles,
            qualifying.Select(c => c.ChartId).ToHashSet(), ranks,
            Gains(request, priced, qualifying), before != null);
    }

    /// <summary>
    ///     One pool's movement plus where it leaves the account standing. The standing is read on
    ///     every pool because PUMBILITY's own rank estimate reads all three boards
    ///     (<c>PlayerRatingSaga.EstimateOfficialRanks</c>) — one indexed count each, in a
    ///     background consumer (D20).
    /// </summary>
    private async Task<HardmodePoolMove> Move(RepriceHardmodePool request, double old, double now, int held,
        ChartType? pool, CancellationToken cancellationToken)
    {
        var standing = await _ratings.GetStanding(request.Mix, pool, request.UserId, cancellationToken);
        return new HardmodePoolMove(old, now, held, standing?.Place, standing?.Field);
    }

    /// <summary>
    ///     What each play in the batch added to the Hardmode combined pool, through the same
    ///     attribution the PUMBILITY gain badge uses — so the two chips on one score line are
    ///     computed the same way and differ only in which pool they measure.
    ///     <para>
    ///         The partial-pool case is where they diverge in practice, and the shared helper
    ///         already handles it: past the leavers a pool was not full, so an entrant displaced
    ///         nothing and banks its whole value. That is why a play worth +12 to PUMBILITY can be
    ///         worth +340 here.
    ///     </para>
    /// </summary>
    private static IReadOnlyDictionary<Guid, double> Gains(RepriceHardmodePool request,
        IReadOnlyList<(RecordedPhoenixScore Record, Chart Chart, double Value)> priced,
        IReadOnlyList<HardmodeChartEntry> qualifying)
    {
        if (request.Changes == null || request.Changes.Count == 0)
            return new Dictionary<Guid, double>();

        var wanted = qualifying.Select(c => c.ChartId).ToHashSet();
        var scoring = ScoringConfiguration.PumbilityScoring(request.Mix, false);

        // A new pass held no seat whatever preceded it — a broken run prices to zero here and was
        // never in the pool, so treating it as a clean prior collapses a real entry to a delta.
        var prior = request.Changes
            .Where(c => wanted.Contains(c.ChartId))
            .GroupBy(c => c.ChartId)
            .ToDictionary(g => g.Key, g => g.Any(c => c.IsNewPass) ? null : g.Select(c => c.OldScore).Max());

        var attributed = priced
            .Select(p => new PumbilityAttribution.Priced(p.Record.ChartId,
                !prior.TryGetValue(p.Record.ChartId, out var old) ? p.Value
                : old == null ? null
                : scoring.GetScore(p.Chart, PhoenixScore.From(old.Value),
                    p.Record.Plate ?? ScoringConfiguration.ExpectedPlateForScore(old.Value), false),
                p.Value))
            .ToArray();

        return PumbilityAttribution.GainsPerChart(attributed, PoolSize);
    }

    public async Task<HardmodePageRecord> Handle(GetHardmodePageQuery request,
        CancellationToken cancellationToken)
    {
        var qualifying = await _hardmodeCharts.GetQualifyingCharts(request.Mix, cancellationToken);
        if (qualifying.Count == 0)
            return new HardmodePageRecord(request.Mix, request.Pool, HardmodePoolTotals.Empty,
                HardmodePoolTotals.Empty, HardmodePoolTotals.Empty, Array.Empty<PoolEntry>(),
                Array.Empty<PoolEntry>(), Array.Empty<TitleRail>(), 0);

        var priced = await Priced(request.Mix, request.UserId, qualifying, cancellationToken);

        var combined = Totals(priced, null);
        var singles = Totals(priced, ChartType.Single);
        var doubles = Totals(priced, ChartType.Double);

        var fifty = Fifty(priced, request.Pool);
        var pool = fifty
            .Select((p, i) => new PoolEntry(i + 1, p.Record.ChartId, p.Record.Score!.Value, p.Record.Plate,
                p.Record.IsBroken, p.Record.RecordedDate, p.Value))
            .ToArray();

        // Everything else the viewer has scored on a qualifying chart. The chart list draws it as
        // its own state — a pass that does not count here — and while the pool is short of fifty
        // this is simply empty, because nothing is being displaced.
        var held = fifty.Select(f => f.Record.ChartId).ToHashSet();
        var outside = priced
            .Where(p => !held.Contains(p.Record.ChartId))
            .OrderByDescending(p => p.Value)
            .Select((p, i) => new PoolEntry(i + 1, p.Record.ChartId, p.Record.Score!.Value, p.Record.Plate,
                p.Record.IsBroken, p.Record.RecordedDate, p.Value))
            .ToArray();

        // The viewer's own ranks come from the stored board rather than being recomputed here:
        // a rank is a fact about everyone, and this page's own totals are not what it is measured
        // against. Both move with an import now (D15); the CHART LIST is the week-old part.
        combined = await Ranked(combined, request.Mix, null, request.UserId, cancellationToken);
        singles = await Ranked(singles, request.Mix, ChartType.Single, request.UserId, cancellationToken);
        doubles = await Ranked(doubles, request.Mix, ChartType.Double, request.UserId, cancellationToken);

        return new HardmodePageRecord(request.Mix, request.Pool, combined, singles, doubles, pool, outside,
            Rails(combined, singles, doubles, request.Mix), qualifying.Count);
    }

    /// <summary>
    ///     The board this viewer may see. The visibility rule lives in the read rather than here
    ///     (D17), so the standing the page prints above the rows is counted over the same
    ///     population and cannot disagree with them.
    /// </summary>
    public async Task<HardmodeBoardRecord> Handle(GetHardmodeBoardQuery request,
        CancellationToken cancellationToken)
    {
        return await _ratings.GetBoard(request.Mix, request.Pool, request.ViewerId, cancellationToken);
    }

    /// <summary>
    ///     One account's qualifying charts, priced. The shared read behind both the page and the
    ///     single-account reprice, so the number a player sees and the number the board stores
    ///     cannot come from two different pricings.
    /// </summary>
    private async Task<IReadOnlyList<(RecordedPhoenixScore Record, Chart Chart, double Value)>> Priced(
        MixEnum mix, Guid userId, IReadOnlyList<HardmodeChartEntry> qualifying,
        CancellationToken cancellationToken)
    {
        var ids = qualifying.Select(c => c.ChartId).ToHashSet();
        var charts = (await _charts.GetCharts(mix, cancellationToken: cancellationToken))
            .Where(c => ids.Contains(c.Id)).ToDictionary(c => c.Id);
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);

        return (await _scores.GetBestScores(mix, userId, cancellationToken))
            .Where(r => r.Score != null && !r.IsBroken && charts.ContainsKey(r.ChartId))
            .Select(r => (Record: r, Chart: charts[r.ChartId],
                Value: scoring.GetScore(charts[r.ChartId], r.Score!.Value,
                    r.Plate ?? ScoringConfiguration.ExpectedPlateForScore(r.Score!.Value), false)))
            .Where(p => p.Value > 0)
            .ToArray();
    }

    private static HardmodePoolTotals Totals(
        IReadOnlyCollection<(RecordedPhoenixScore Record, Chart Chart, double Value)> priced, ChartType? pool)
    {
        var fifty = Fifty(priced, pool);
        return new HardmodePoolTotals(Sum(fifty.Select(f => f.Value)), fifty.Count, null, null);
    }

    private static IReadOnlyList<(RecordedPhoenixScore Record, Chart Chart, double Value)> Fifty(
        IEnumerable<(RecordedPhoenixScore Record, Chart Chart, double Value)> priced, ChartType? pool)
    {
        return priced
            .Where(p => pool == null || p.Chart.Type == pool)
            .OrderByDescending(p => p.Value)
            .ThenBy(p => p.Record.ChartId)
            .Take(PoolSize)
            .ToArray();
    }

    /// <summary>Never rounded — a pool is fifty fractional contributions and the page spends the precision.</summary>
    private static double Sum(IEnumerable<double> values)
    {
        return values.Sum();
    }

    /// <summary>
    ///     One account's three pools, each the best fifty of its OWN set with a held count from
    ///     that same set. A doubles pool holding twelve charts must not read "50 of 50" because
    ///     the combined pool happens to be full.
    /// </summary>
    private static HardmodeRatingRow Row(Guid userId,
        IReadOnlyCollection<(ChartType Type, double Value)> valued)
    {
        var singles = valued.Where(v => v.Type == ChartType.Single).Select(v => v.Value).ToArray();
        var doubles = valued.Where(v => v.Type == ChartType.Double).Select(v => v.Value).ToArray();
        return new HardmodeRatingRow(userId,
            Top(valued.Select(v => v.Value)), Top(singles), Top(doubles),
            Math.Min(PoolSize, valued.Count), Math.Min(PoolSize, singles.Length),
            Math.Min(PoolSize, doubles.Length));
    }

    /// <summary>
    ///     The best fifty, summed — the same rule the page's <see cref="Fifty" /> applies, and the
    ///     official half's own Top.
    ///     <para>
    ///         Summing every qualifying chart instead does not overstate a pool slightly; it
    ///         prices a different, unbounded thing. It put board totals ABOVE the same player's
    ///         PUMBILITY, which cannot happen: the qualifying charts are a subset of the ones
    ///         PUMBILITY already picks its own fifty from, so a Hardmode pool is bounded by it.
    ///         The held count read "50 / 50" throughout while that was true, because it was
    ///         capped rather than measured — so the row looked ordinary beside a total that
    ///         was nearly three times real.
    ///     </para>
    /// </summary>
    private static double Top(IEnumerable<double> values)
    {
        return values.OrderByDescending(v => v).Take(PoolSize).Sum();
    }

    private async Task<HardmodePoolTotals> Ranked(HardmodePoolTotals totals, MixEnum mix, ChartType? pool,
        Guid userId, CancellationToken cancellationToken)
    {
        var standing = await _ratings.GetStanding(mix, pool, userId, cancellationToken);
        return standing == null
            ? totals
            : totals with { Rank = standing.Value.Place, Field = standing.Value.Field };
    }

    /// <summary>
    ///     The Phoenix 2 ladders, asked of the Hardmode pool (D10). The same record the Breakdown
    ///     page's rails draw, so the component needs no Hardmode branch — with no ask examples
    ///     and no projected average, because both belong to the Play tab's suggestions and this
    ///     page makes none.
    /// </summary>
    private static IReadOnlyList<TitleRail> Rails(HardmodePoolTotals combined, HardmodePoolTotals singles,
        HardmodePoolTotals doubles, MixEnum mix)
    {
        if (mix != MixEnum.Phoenix2) return Array.Empty<TitleRail>();
        var rails = new List<TitleRail>();
        Add(PumbilityPool.Total, combined);
        Add(PumbilityPool.Singles, singles);
        Add(PumbilityPool.Doubles, doubles);
        return rails;

        void Add(PumbilityPool ladder, HardmodePoolTotals totals)
        {
            var rungs = Phoenix2TitleList.BuildList().OfType<Phoenix2PumbilityTitle>()
                .Where(t => t.Pool == ladder)
                .OrderBy(t => t.CompletionRequired)
                .ToArray();
            if (rungs.Length == 0) return;

            var held = rungs.LastOrDefault(t => t.CompletionRequired <= totals.Total);
            var next = rungs.FirstOrDefault(t => t.CompletionRequired > totals.Total);
            rails.Add(new TitleRail(ladder, totals.Total, held?.Name.ToString(),
                held?.CompletionRequired ?? 0, next?.Name.ToString(), next?.CompletionRequired,
                next == null ? 0 : next.CompletionRequired / (double)PoolSize,
                totals.Held == 0 ? 0 : totals.Total / totals.Held, null, Array.Empty<AskExample>(), null));
        }
    }
}
