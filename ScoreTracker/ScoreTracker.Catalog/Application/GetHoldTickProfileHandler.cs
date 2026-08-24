using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Catalog.Application;

/// <summary>
///     Derives the hold-tick picture (docs/design/phoenix-score-calculator.md D13): a chart's
///     ticks are its judged note count minus its banked simfile tap rows. The simfiles'
///     *holds* are pre-Phoenix and never read — taps are the half that survives re-balances.
///     Two gates drop charts the subtraction cannot be trusted on: implied ticks below zero,
///     and a simfile with no holds whose total still exceeds its taps — both mean the chart
///     was re-stepped out from under the simfile.
/// </summary>
internal sealed class GetHoldTickProfileHandler(
    IMediator mediator,
    IChartSkillMetricRepository metrics,
    IMemoryCache cache)
    : IRequestHandler<GetHoldTickProfileQuery, HoldTickProfile>
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(24);

    /// <summary>Percentiles need a floor to mean anything; smaller levels stay off the chart.</summary>
    private const int MinChartsPerLevel = 5;

    public async Task<HoldTickProfile> Handle(GetHoldTickProfileQuery request,
        CancellationToken cancellationToken)
    {
        return (await cache.GetOrCreateAsync($"HoldTickProfile__{request.Mix}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            return await Build(request.Mix, cancellationToken);
        }))!;
    }

    private async Task<HoldTickProfile> Build(MixEnum mix, CancellationToken cancellationToken)
    {
        var charts = (await mediator.Send(new GetChartsQuery(mix), cancellationToken))
            .Where(c => c.Type is ChartType.Single or ChartType.Double)
            .ToArray();
        // Phoenix 2's note counts refill from play; a chart still unobserved there reads its
        // Phoenix count (owner ruling — the totals changed on 11 of 2,042 charts judged on
        // both mixes, too sparse to caveat).
        var fallbackCounts = mix == MixEnum.Phoenix
            ? new Dictionary<Guid, int>()
            : (await mediator.Send(new GetChartsQuery(MixEnum.Phoenix), cancellationToken))
            .Where(c => c.NoteCount != null)
            .ToDictionary(c => c.Id, c => c.NoteCount!.Value);
        var banked = await metrics.GetMetricsByChart(PiuCenterMetrics.Source, cancellationToken);

        var measured = new List<HoldTickChartStat>();
        foreach (var chart in charts)
        {
            var noteCount = chart.NoteCount
                            ?? (fallbackCounts.TryGetValue(chart.Id, out var fallback) ? fallback : (int?)null);
            if (noteCount is not > 0) continue;
            if (!banked.TryGetValue(chart.Id, out var chartMetrics)) continue;
            var tapRows = MetricValue(chartMetrics, PiuCenterMetrics.TapRows);
            if (tapRows == null) continue;
            var holdRows = MetricValue(chartMetrics, PiuCenterMetrics.HoldRows) ?? 0;

            var ticks = noteCount.Value - tapRows.Value;
            if (ticks < 0) continue;
            if (holdRows == 0 && ticks > 0) continue;

            measured.Add(new HoldTickChartStat(chart.Id, chart.Song.Name.ToString(), chart.Type,
                (int)chart.Level, noteCount.Value, ticks, (double)ticks / noteCount.Value));
        }

        var levels = measured
            .GroupBy(m => m.Level)
            .Where(level => level.Count() >= MinChartsPerLevel)
            .OrderBy(level => level.Key)
            .Select(level =>
            {
                var shares = level.Select(m => m.Share).OrderBy(share => share).ToArray();
                return new HoldTickLevelStat(level.Key, shares.Length, Quantile(shares, .5),
                    Quantile(shares, .1), Quantile(shares, .9));
            })
            .ToArray();

        return new HoldTickProfile(
            levels,
            measured.OrderByDescending(m => m.Share).ThenByDescending(m => m.HoldTicks).Take(6).ToArray(),
            measured.Where(m => m.Level >= 15).OrderBy(m => m.Share).ThenBy(m => m.HoldTicks).Take(5)
                .ToArray(),
            measured.Count);
    }

    private static int? MetricValue(IReadOnlyList<ChartSkillMetric> chartMetrics, string name)
    {
        var metric = chartMetrics.FirstOrDefault(m => m.MetricName == name);
        return metric == null ? null : (int)metric.Value;
    }

    private static double Quantile(double[] sortedValues, double quantile)
    {
        var index = (int)Math.Round(quantile * (sortedValues.Length - 1), MidpointRounding.AwayFromZero);
        return sortedValues[Math.Clamp(index, 0, sortedValues.Length - 1)];
    }
}
