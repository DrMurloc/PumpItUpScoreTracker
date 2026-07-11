using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.Catalog.Contracts.Messages;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Catalog.Application;

/// <summary>
///     The weekly piucenter crawl (design doc tier-lists-overhaul §8a). One pass:
///     1. Reconcile the alias map against their chart table — new keys auto-match by
///        normalized song/artist/type/level/variant or park unresolved (ChartId null,
///        the admin grid's queue); NotFound candidates whose key appeared flip to Auto.
///     2. Fetch per-chart analysis for resolved aliases missing the current data
///        release (the client throttles; a killed run resumes at the gap set).
///     3. Regenerate every chart's Skill tags from the banked metrics — including
///        clearing tags on charts piucenter has nothing for. The pre-crawler hand tags
///        live on in scores.ChartSkillArchive only (owner-locked full replace).
/// </summary>
internal sealed class PiuCenterCrawlSaga : IConsumer<CrawlPiuCenterCommand>
{
    private readonly IExternalChartAliasRepository _aliases;
    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly ILogger<PiuCenterCrawlSaga> _logger;
    private readonly IChartSkillMetricRepository _metrics;
    private readonly IPiuCenterClient _piuCenter;

    public PiuCenterCrawlSaga(IPiuCenterClient piuCenter, IExternalChartAliasRepository aliases,
        IChartSkillMetricRepository metrics, IChartRepository charts, IDateTimeOffsetAccessor clock,
        ILogger<PiuCenterCrawlSaga> logger)
    {
        _piuCenter = piuCenter;
        _aliases = aliases;
        _metrics = metrics;
        _charts = charts;
        _clock = clock;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CrawlPiuCenterCommand> context)
    {
        var cancellationToken = context.CancellationToken;
        var now = _clock.Now;
        var version = decimal.Parse(await _piuCenter.GetDataVersion(cancellationToken));
        var table = await _piuCenter.GetChartTable(cancellationToken);
        var phoenixCharts = (await _charts.GetCharts(MixEnum.Phoenix, cancellationToken: cancellationToken))
            .ToArray();

        var aliases = await ReconcileAliases(table, phoenixCharts, now, cancellationToken);
        var resolved = aliases
            .Where(a => a.ChartId != null && a.Status != ExternalAliasStatus.NotFound)
            .Where(a => table.Any(t => t.ExternalKey == a.ExternalKey))
            .GroupBy(a => a.ExternalKey).Select(g => g.First())
            .ToArray();

        await FillMetricGaps(resolved, table, version, cancellationToken);
        await RegenerateSkillTags(resolved, phoenixCharts, cancellationToken);
    }

    private async Task<IReadOnlyList<ExternalChartAlias>> ReconcileAliases(
        IReadOnlyList<PiuCenterChartListing> table, IReadOnlyList<Chart> phoenixCharts, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var known = (await _aliases.GetAliases(PiuCenterMetrics.Source, cancellationToken))
            .ToDictionary(a => a.ExternalKey);
        var matchIndex = BuildMatchIndex(phoenixCharts);
        var alreadyResolved = known.Values
            .Where(a => a.ChartId != null && a.Status != ExternalAliasStatus.NotFound)
            .Select(a => a.ChartId!.Value)
            .ToHashSet();

        var updates = new List<ExternalChartAlias>();
        foreach (var listing in table)
            if (known.TryGetValue(listing.ExternalKey, out var existing))
            {
                // A NotFound candidate key that now exists upstream: the chart got analyzed.
                if (existing.Status == ExternalAliasStatus.NotFound)
                    updates.Add(existing with { Status = ExternalAliasStatus.Auto, LastCheckedAt = now });
            }
            else
            {
                var chartId = TryMatch(matchIndex, listing);
                if (chartId != null && !alreadyResolved.Add(chartId.Value)) chartId = null;
                updates.Add(new ExternalChartAlias(listing.ExternalKey, chartId, ExternalAliasStatus.Auto, now));
                if (chartId == null)
                    _logger.LogInformation("piucenter key {Key} has no auto-match — parked for admin resolution",
                        listing.ExternalKey);
            }

        if (updates.Count > 0)
        {
            await _aliases.SaveAliases(PiuCenterMetrics.Source, updates, cancellationToken);
            foreach (var update in updates) known[update.ExternalKey] = update;
        }

        return known.Values.ToArray();
    }

    private async Task FillMetricGaps(IReadOnlyList<ExternalChartAlias> resolved,
        IReadOnlyList<PiuCenterChartListing> table, decimal version, CancellationToken cancellationToken)
    {
        var listingByKey = table.ToDictionary(t => t.ExternalKey);
        var versionByChart =
            (await _metrics.GetMetrics(resolved.Select(a => a.ChartId!.Value), PiuCenterMetrics.Source,
                cancellationToken))
            .Where(m => m.MetricName == PiuCenterMetrics.DataVersion)
            .ToDictionary(m => m.ChartId, m => m.Value);
        var gaps = resolved
            .Where(a => !versionByChart.TryGetValue(a.ChartId!.Value, out var have) || have != version)
            .ToArray();
        if (gaps.Length == 0) return;

        _logger.LogInformation("piucenter crawl: fetching {Count} chart pages for data release {Version}",
            gaps.Length, version);
        var practiceByKey = (await _piuCenter.GetPracticeLists(cancellationToken))
            .GroupBy(e => e.ExternalKey)
            .ToDictionary(g => g.Key, g => g.ToArray());
        var predictions = await _piuCenter.GetDifficultyPredictions(cancellationToken);

        var fetched = 0;
        foreach (var alias in gaps)
        {
            var page = await _piuCenter.GetChartPage(alias.ExternalKey, cancellationToken);
            if (page == null)
            {
                _logger.LogWarning("piucenter chart page {Key} is listed but missing — skipped", alias.ExternalKey);
                continue;
            }

            var rows = BuildMetrics(alias.ChartId!.Value, version, page,
                listingByKey.GetValueOrDefault(alias.ExternalKey),
                practiceByKey.GetValueOrDefault(alias.ExternalKey),
                predictions.TryGetValue(alias.ExternalKey, out var prediction) ? prediction : null);
            await _metrics.ReplaceChartMetrics(alias.ChartId.Value, PiuCenterMetrics.Source, rows, cancellationToken);
            fetched++;
        }

        _logger.LogInformation("piucenter crawl: banked metrics for {Fetched}/{Total} gap charts", fetched,
            gaps.Length);
    }

    private async Task RegenerateSkillTags(IReadOnlyList<ExternalChartAlias> resolved,
        IReadOnlyList<Chart> phoenixCharts, CancellationToken cancellationToken)
    {
        var chartById = phoenixCharts.ToDictionary(c => c.Id);
        var metricsByChart =
            (await _metrics.GetMetrics(resolved.Select(a => a.ChartId!.Value), PiuCenterMetrics.Source,
                cancellationToken))
            .GroupBy(m => m.ChartId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ChartSkillMetric>)g.ToArray());

        // Fast/Slow cutoffs are folder-relative: NPS quartiles within (type, level).
        var cutoffs = metricsByChart
            .Where(kv => chartById.ContainsKey(kv.Key))
            .Select(kv => (Chart: chartById[kv.Key],
                Nps: kv.Value.FirstOrDefault(m => m.MetricName == PiuCenterMetrics.Nps)?.Value))
            .Where(x => x.Nps != null)
            .GroupBy(x => (x.Chart.Type, Level: (int)x.Chart.Level))
            .ToDictionary(g => g.Key, g =>
            {
                var sorted = g.Select(x => x.Nps!.Value).OrderBy(v => v).ToArray();
                return (Fast: Quantile(sorted, PiuCenterSkillMapper.FastNpsQuantile),
                    Slow: Quantile(sorted, PiuCenterSkillMapper.SlowNpsQuantile));
            });

        var desired = new Dictionary<Guid, ChartSkillsRecord>();
        foreach (var (chartId, chartMetrics) in metricsByChart)
        {
            (decimal Fast, decimal Slow)? folder = chartById.TryGetValue(chartId, out var chart) &&
                                                   cutoffs.TryGetValue((chart.Type, (int)chart.Level), out var c)
                ? c
                : null;
            desired[chartId] = PiuCenterSkillMapper.Map(chartId, chartMetrics, folder?.Fast, folder?.Slow);
        }

        // Charts with tags but no piucenter data lose them — the hand-tag purge.
        var current = (await _charts.GetChartSkills(cancellationToken)).ToDictionary(r => r.ChartId);
        foreach (var record in current.Values)
            if (!desired.ContainsKey(record.ChartId) && chartById.ContainsKey(record.ChartId))
                desired[record.ChartId] = new ChartSkillsRecord(record.ChartId, Array.Empty<Skill>(),
                    Array.Empty<Skill>());

        var changed = 0;
        foreach (var record in desired.Values)
        {
            if (current.TryGetValue(record.ChartId, out var existing) && SameTags(existing, record)) continue;
            await _charts.SaveChartSkills(record, cancellationToken);
            changed++;
        }

        _logger.LogInformation("piucenter crawl: regenerated skill tags — {Changed} charts changed of {Total}",
            changed, desired.Count);
    }

    private static bool SameTags(ChartSkillsRecord stored, ChartSkillsRecord desired)
    {
        // Stored records read ContainsSkills as ALL rows (highlighted included); desired
        // records keep the two sets disjoint — union before comparing.
        var storedAll = stored.ContainsSkills.ToHashSet();
        var desiredAll = desired.ContainsSkills.Concat(desired.HighlightsSkill).ToHashSet();
        return storedAll.SetEquals(desiredAll) &&
               stored.HighlightsSkill.ToHashSet().SetEquals(desired.HighlightsSkill.ToHashSet());
    }

    private static decimal Quantile(decimal[] sortedValues, double quantile)
    {
        var index = (int)Math.Round(quantile * (sortedValues.Length - 1), MidpointRounding.AwayFromZero);
        return sortedValues[Math.Clamp(index, 0, sortedValues.Length - 1)];
    }

    private static List<ChartSkillMetric> BuildMetrics(Guid chartId, decimal version, PiuCenterChartPage page,
        PiuCenterChartListing? listing, IReadOnlyList<PiuCenterPracticeEntry>? practice, decimal? prediction)
    {
        var rows = new List<ChartSkillMetric>
        {
            new(chartId, PiuCenterMetrics.DataVersion, version, null)
        };
        var nps = page.Nps ?? listing?.Nps;
        if (nps != null) rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.Nps, nps.Value, null));
        if (listing != null)
        {
            rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.SustainTime, listing.SustainTime, null));
            rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.TimeUnderTension, listing.TimeUnderTension,
                null));
        }

        if (prediction != null)
            rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.DifficultyPrediction, prediction.Value, null));

        for (var i = 0; i < page.SkillSummary.Count; i++)
            rows.Add(new ChartSkillMetric(chartId, $"{PiuCenterMetrics.Top3Prefix}{page.SkillSummary[i]}", i + 1,
                null));

        if (page.SegmentCount > 0)
            foreach (var (skill, count) in page.SegmentSkillCounts)
                rows.Add(new ChartSkillMetric(chartId, $"{PiuCenterMetrics.BadgeFractionPrefix}{skill}",
                    Math.Round((decimal)count / page.SegmentCount, 4), null));

        foreach (var skill in page.LastSegmentSkills)
            rows.Add(new ChartSkillMetric(chartId, $"{PiuCenterMetrics.LastSegmentPrefix}{skill}", 1, null));

        foreach (var (label, count) in page.RareSkillCounts)
            rows.Add(new ChartSkillMetric(chartId, Truncate($"{PiuCenterMetrics.RarePrefix}{label}", 64), count,
                null));

        if (practice != null)
            foreach (var entry in practice)
                rows.Add(new ChartSkillMetric(chartId,
                    Truncate($"{PiuCenterMetrics.PracticeRankPrefix}{entry.Skill}", 64), entry.Rank, null));

        return rows
            .GroupBy(r => r.MetricName)
            .Select(g => g.First())
            .ToList();
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    // --- alias auto-matching (tier-1 of the seeding pass, for future upstream keys) ---

    private static Dictionary<string, Guid?> BuildMatchIndex(IReadOnlyList<Chart> phoenixCharts)
    {
        var index = new Dictionary<string, Guid?>();
        foreach (var chart in phoenixCharts)
        {
            var sord = chart.Type switch
            {
                ChartType.Single => "singles",
                ChartType.Double => "doubles",
                _ => null
            };
            if (sord == null) continue;
            var variant = chart.Song.Type switch
            {
                SongType.Remix => "REMIX",
                SongType.ShortCut => "SHORTCUT",
                SongType.FullSong => "FULLSONG",
                _ => "ARCADE"
            };
            var key =
                $"{PiuCenterSkillMapper.Normalize(chart.Song.Name)}|{PiuCenterSkillMapper.Normalize(chart.Song.Artist)}|{sord}|{(int)chart.Level}|{variant}";
            // Ambiguous keys match nothing — better parked than misbound.
            index[key] = index.ContainsKey(key) ? null : chart.Id;
        }

        return index;
    }

    private static Guid? TryMatch(IReadOnlyDictionary<string, Guid?> matchIndex, PiuCenterChartListing listing)
    {
        if (!PiuCenterKeyParser.TryParse(listing.ExternalKey, out var parts)) return null;
        var sord = listing.Type == ChartType.Single ? "singles" : "doubles";
        // Our charts don't carry a half-double marker — their HALFDOUBLE_<X> keys map
        // onto plain Double charts with variant <X> (same as the seeding pass's tier 3a).
        var variant = listing.Variant.StartsWith("HALFDOUBLE_", StringComparison.Ordinal)
            ? listing.Variant["HALFDOUBLE_".Length..]
            : listing.Variant;
        var key =
            $"{PiuCenterSkillMapper.Normalize(parts.SongPart)}|{PiuCenterSkillMapper.Normalize(parts.ArtistPart)}|{sord}|{listing.Level}|{variant}";
        return matchIndex.GetValueOrDefault(key);
    }
}
