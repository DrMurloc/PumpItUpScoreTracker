using MediatR;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Catalog.Application;

/// <summary>
///     The SRP drawer's autocomplete dictionaries: the badge cloud (distinct top-3 keys in
///     the banked metrics), song artists, and step artists. All served off caches the
///     chart and metric repositories already hold.
/// </summary>
internal sealed class SearchVocabularyHandler :
    IRequestHandler<GetSearchBadgesQuery, IReadOnlyList<ChartBadge>>,
    IRequestHandler<GetSearchArtistsQuery, IReadOnlyList<string>>,
    IRequestHandler<GetSearchStepArtistsQuery, IReadOnlyList<string>>
{
    private readonly IChartRepository _charts;
    private readonly IChartSkillMetricRepository _metrics;

    public SearchVocabularyHandler(IChartRepository charts, IChartSkillMetricRepository metrics)
    {
        _charts = charts;
        _metrics = metrics;
    }

    public async Task<IReadOnlyList<ChartBadge>> Handle(GetSearchBadgesQuery request,
        CancellationToken cancellationToken)
    {
        var metrics = await _metrics.GetMetricsByChart(PiuCenterMetrics.Source, cancellationToken);
        return metrics.Values
            .SelectMany(rows => rows)
            .Where(m => m.MetricName.StartsWith(PiuCenterMetrics.Top3Prefix, StringComparison.Ordinal))
            .Select(m => m.MetricName[PiuCenterMetrics.Top3Prefix.Length..])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(k => new ChartBadge(k, PiuCenterBadges.DisplayName(k), PiuCenterBadges.CategoryFor(k)))
            .OrderBy(b => b.Category == null ? 1 : 0)
            .ThenBy(b => b.Category)
            .ThenBy(b => b.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> Handle(GetSearchArtistsQuery request,
        CancellationToken cancellationToken)
    {
        return await DistinctOverScope(request.Mix, request.AllMixes,
            c => c.Song.Artist.ToString(), cancellationToken);
    }

    public async Task<IReadOnlyList<string>> Handle(GetSearchStepArtistsQuery request,
        CancellationToken cancellationToken)
    {
        return await DistinctOverScope(request.Mix, request.AllMixes,
            c => c.StepArtist?.ToString(), cancellationToken);
    }

    private async Task<IReadOnlyList<string>> DistinctOverScope(MixEnum mix, bool allMixes,
        Func<Chart, string?> value, CancellationToken cancellationToken)
    {
        var mixes = allMixes
            ? (await _charts.GetChartMixLevels(cancellationToken)).Select(l => l.Mix).Distinct().ToArray()
            : new[] { mix };
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var scopeMix in mixes)
        foreach (var chart in await _charts.GetCharts(scopeMix, cancellationToken: cancellationToken))
        {
            var v = value(chart);
            if (!string.IsNullOrWhiteSpace(v)) values.Add(v);
        }

        return values.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
