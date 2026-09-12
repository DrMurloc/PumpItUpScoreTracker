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
    IRequestHandler<GetHardmodeBoardQuery, IReadOnlyList<HardmodeBoardRow>>
{
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
        var rows = valued.Select(kv => new HardmodeRatingRow(kv.Key,
                Sum(kv.Value.Select(v => v.Value)),
                Sum(kv.Value.Where(v => v.Type == ChartType.Single).Select(v => v.Value)),
                Sum(kv.Value.Where(v => v.Type == ChartType.Double).Select(v => v.Value)),
                Math.Min(PoolSize, kv.Value.Count)))
            .ToArray();
        await _ratings.Save(mix, rows, cancellationToken);
        _logger.LogInformation("Hardmode ratings for {Mix}: {Accounts} accounts over {Charts} charts", mix,
            rows.Length, qualifying.Count);
    }

    public async Task<HardmodePageRecord> Handle(GetHardmodePageQuery request,
        CancellationToken cancellationToken)
    {
        var qualifying = await _hardmodeCharts.GetQualifyingCharts(request.Mix, cancellationToken);
        if (qualifying.Count == 0)
            return new HardmodePageRecord(request.Mix, request.Pool, HardmodePoolTotals.Empty,
                HardmodePoolTotals.Empty, HardmodePoolTotals.Empty, Array.Empty<PoolEntry>(),
                Array.Empty<TitleRail>(), 0);

        var ids = qualifying.Select(c => c.ChartId).ToHashSet();
        var charts = (await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken))
            .Where(c => ids.Contains(c.Id)).ToDictionary(c => c.Id);
        var scoring = ScoringConfiguration.PumbilityScoring(request.Mix, false);

        var priced = (await _scores.GetBestScores(request.Mix, request.UserId, cancellationToken))
            .Where(r => r.Score != null && !r.IsBroken && charts.ContainsKey(r.ChartId))
            .Select(r => (Record: r, Chart: charts[r.ChartId],
                Value: scoring.GetScore(charts[r.ChartId], r.Score!.Value,
                    r.Plate ?? ScoringConfiguration.ExpectedPlateForScore(r.Score!.Value), false)))
            .Where(p => p.Value > 0)
            .ToArray();

        var combined = Totals(priced, null);
        var singles = Totals(priced, ChartType.Single);
        var doubles = Totals(priced, ChartType.Double);

        var pool = Fifty(priced, request.Pool)
            .Select((p, i) => new PoolEntry(i + 1, p.Record.ChartId, p.Record.Score!.Value, p.Record.Plate,
                p.Record.IsBroken, p.Record.RecordedDate, p.Value))
            .ToArray();

        // The viewer's own ranks come from the stored board rather than being recomputed here:
        // a rank is a fact about everyone, and the one thing on this page that is a week old.
        combined = await Ranked(combined, request.Mix, null, request.UserId, cancellationToken);
        singles = await Ranked(singles, request.Mix, ChartType.Single, request.UserId, cancellationToken);
        doubles = await Ranked(doubles, request.Mix, ChartType.Double, request.UserId, cancellationToken);

        return new HardmodePageRecord(request.Mix, request.Pool, combined, singles, doubles, pool,
            Rails(combined, singles, doubles, request.Mix), qualifying.Count);
    }

    public async Task<IReadOnlyList<HardmodeBoardRow>> Handle(GetHardmodeBoardQuery request,
        CancellationToken cancellationToken)
    {
        return await _ratings.GetBoard(request.Mix, request.Pool, cancellationToken);
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

    private async Task<HardmodePoolTotals> Ranked(HardmodePoolTotals totals, MixEnum mix, ChartType? pool,
        Guid userId, CancellationToken cancellationToken)
    {
        var board = await _ratings.GetBoard(mix, pool, cancellationToken);
        var mine = board.FirstOrDefault(r => r.UserId == userId);
        return mine == null ? totals : totals with { Rank = mine.Place, Field = board.Count };
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
