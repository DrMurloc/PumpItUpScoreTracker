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
    IRequestHandler<GetSearchArtistsQuery, IReadOnlyList<ChartSearchVocabularyEntry>>,
    IRequestHandler<GetSearchStepArtistsQuery, IReadOnlyList<ChartSearchVocabularyEntry>>,
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
        var charts = (await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken)).ToArray();
        var chartIds = charts.Select(c => c.Id).ToHashSet();

        // NPS is banked catalogue-wide, so narrow it to the mix's own charts or the slider
        // travels over eras that aren't on screen.
        var npsValues = (await _metrics.GetMetricsByChart(PiuCenterMetrics.Source, cancellationToken))
            .Where(kv => chartIds.Contains(kv.Key))
            .Select(kv => kv.Value.FirstOrDefault(m => m.MetricName == PiuCenterMetrics.Nps)?.Value)
            .Where(v => v != null)
            .Select(v => v!.Value)
            .ToArray();

        var scoringLevels = request.Mix.UsesLegacyScoring()
            ? Array.Empty<double>()
            : (await _scoringLevels.GetScoringLevels(request.Mix, cancellationToken)).Values.ToArray();

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
            scoringLevels.Length == 0 ? null : Math.Floor(scoringLevels.Min()),
            scoringLevels.Length == 0 ? null : Math.Ceiling(scoringLevels.Max()));
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
            .Select(k => new ChartBadge(k, BadgeLabels.DisplayName(k), BadgeLabels.CategoryFor(k)))
            // Grouped by family, then by name inside it, so the cloud reads as five colour
            // blocks — an unfamiliar badge is placed by the company it keeps. Uncategorised
            // (a badge piucenter added that the table has not learned) sorts last.
            .OrderBy(b => b.Category == null ? 1 : 0)
            .ThenBy(b => b.Category)
            .ThenBy(b => b.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<ChartSearchVocabularyEntry>> Handle(GetSearchArtistsQuery request,
        CancellationToken cancellationToken)
    {
        return await CountedInMix(request.Mix, c => c.Song.Artist.ToString(), cancellationToken);
    }

    public async Task<IReadOnlyList<ChartSearchVocabularyEntry>> Handle(GetSearchStepArtistsQuery request,
        CancellationToken cancellationToken)
    {
        return await CountedInMix(request.Mix, c => c.StepArtist?.ToString(), cancellationToken);
    }

    /// <summary>
    ///     Distinct values with their chart counts. The first spelling encountered wins as the
    ///     label — matching is case-insensitive, so "BanYa" and "Banya" are one artist with one
    ///     total rather than two half-counts.
    /// </summary>
    private async Task<IReadOnlyList<ChartSearchVocabularyEntry>> CountedInMix(MixEnum mix,
        Func<Chart, string?> value, CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, (string Label, int Count)>(StringComparer.OrdinalIgnoreCase);
        foreach (var chart in await _charts.GetCharts(mix, cancellationToken: cancellationToken))
        {
            var v = value(chart);
            if (string.IsNullOrWhiteSpace(v)) continue;
            counts[v] = counts.TryGetValue(v, out var seen)
                ? (seen.Label, seen.Count + 1)
                : (v, 1);
        }

        return counts.Values
            .OrderBy(e => e.Label, StringComparer.OrdinalIgnoreCase)
            .Select(e => new ChartSearchVocabularyEntry(e.Label, e.Count))
            .ToArray();
    }
}
