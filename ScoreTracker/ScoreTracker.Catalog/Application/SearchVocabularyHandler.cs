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
    IRequestHandler<GetSearchStepArtistsQuery, IReadOnlyList<string>>,
    IRequestHandler<GetSearchRangesQuery, ChartSearchRanges>
{
    private readonly IChartRepository _charts;
    private readonly IChartSkillMetricRepository _metrics;
    private readonly IChartScoringLevelRepository _scoringLevels;

    public SearchVocabularyHandler(IChartRepository charts, IChartSkillMetricRepository metrics,
        IChartScoringLevelRepository scoringLevels)
    {
        _charts = charts;
        _metrics = metrics;
        _scoringLevels = scoringLevels;
    }

    public async Task<ChartSearchRanges> Handle(GetSearchRangesQuery request,
        CancellationToken cancellationToken)
    {
        var mixes = await ScopeMixes(request.Mix, request.AllMixes, cancellationToken);
        var charts = new List<Chart>();
        foreach (var mix in mixes)
            charts.AddRange(await _charts.GetCharts(mix, cancellationToken: cancellationToken));

        var npsValues = (await _metrics.GetMetricsByChart(PiuCenterMetrics.Source, cancellationToken))
            .Values
            .Select(rows => rows.FirstOrDefault(m => m.MetricName == PiuCenterMetrics.Nps)?.Value)
            .Where(v => v != null)
            .Select(v => v!.Value)
            .ToArray();

        var scoringLevels = new List<double>();
        foreach (var mix in mixes.Where(m => !m.UsesLegacyScoring()))
            scoringLevels.AddRange((await _scoringLevels.GetScoringLevels(mix, cancellationToken)).Values);

        var bpms = charts.Where(c => c.Song.Bpm != null).Select(c => c.Song.Bpm!.Value).ToArray();
        var notes = charts.Where(c => c.NoteCount != null).Select(c => c.NoteCount!.Value).ToArray();
        var seconds = charts.Select(c => (int)c.Song.Duration.TotalSeconds).Where(s => s > 0).ToArray();

        return new ChartSearchRanges(
            bpms.Length == 0 ? null : (int)Math.Floor(bpms.Min(b => b.Min)),
            bpms.Length == 0 ? null : (int)Math.Ceiling(bpms.Max(b => b.Max)),
            npsValues.Length == 0 ? null : Math.Floor(npsValues.Min()),
            npsValues.Length == 0 ? null : Math.Ceiling(npsValues.Max()),
            notes.Length == 0 ? null : notes.Min(),
            notes.Length == 0 ? null : notes.Max(),
            seconds.Length == 0 ? null : seconds.Min(),
            seconds.Length == 0 ? null : seconds.Max(),
            scoringLevels.Count == 0 ? null : Math.Floor(scoringLevels.Min()),
            scoringLevels.Count == 0 ? null : Math.Ceiling(scoringLevels.Max()));
    }

    private async Task<IReadOnlyList<MixEnum>> ScopeMixes(MixEnum mix, bool allMixes,
        CancellationToken cancellationToken)
    {
        return allMixes
            ? (await _charts.GetChartMixLevels(cancellationToken)).Select(l => l.Mix).Distinct().ToArray()
            : new[] { mix };
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
        var mixes = await ScopeMixes(mix, allMixes, cancellationToken);
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
