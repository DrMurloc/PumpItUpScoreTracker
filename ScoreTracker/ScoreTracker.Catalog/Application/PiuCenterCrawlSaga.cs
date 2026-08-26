using System.IO.Compression;
using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.Catalog.Contracts.Events;
using ScoreTracker.Catalog.Contracts.Messages;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Data.Apis;
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
///     3. Rebuild every folder's per-badge baselines from the banked metrics, and announce
///        the ingestion so anything derived from step analysis can recompute
///        (docs/design/chart-identity.md §5).
///     The snapshot import runs the same pipeline from an uploaded zip of a data
///     release instead of HTTP — the zero-crawl bootstrap path.
/// </summary>
internal sealed class PiuCenterCrawlSaga : IConsumer<CrawlPiuCenterCommand>,
    IConsumer<ImportPiuCenterSnapshotCommand>
{
    /// <summary>
    ///     The catalog every piucenter key is matched against. It has to be the CURRENT mix,
    ///     because the match key carries the chart's level and a level moves between mixes: read
    ///     against Phoenix, a Phoenix 2 data release mismatches every chart whose level shifted
    ///     and misses every Phoenix 2 song outright — which is why a P2 snapshot appeared to
    ///     upload and bank nothing.
    /// </summary>
    private const MixEnum MatchCatalog = MixEnum.Phoenix2;

    private readonly IExternalChartAliasRepository _aliases;
    private readonly IChartFolderBaselineRepository _baselines;
    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly ILogger<PiuCenterCrawlSaga> _logger;
    private readonly IChartSkillMetricRepository _metrics;
    private readonly IPiuCenterClient _piuCenter;

    public PiuCenterCrawlSaga(IPiuCenterClient piuCenter, IExternalChartAliasRepository aliases,
        IChartSkillMetricRepository metrics, IChartRepository charts, IDateTimeOffsetAccessor clock,
        IChartFolderBaselineRepository baselines, ILogger<PiuCenterCrawlSaga> logger)
    {
        _piuCenter = piuCenter;
        _aliases = aliases;
        _metrics = metrics;
        _charts = charts;
        _clock = clock;
        _baselines = baselines;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CrawlPiuCenterCommand> context)
    {
        var cancellationToken = context.CancellationToken;
        var now = _clock.Now;
        var version = decimal.Parse(await _piuCenter.GetDataVersion(cancellationToken));
        var table = await _piuCenter.GetChartTable(cancellationToken);
        var catalogCharts = (await _charts.GetCharts(MatchCatalog, cancellationToken: cancellationToken))
            .ToArray();

        var aliases = await ReconcileAliases(table, catalogCharts, now, cancellationToken);
        var resolved = ResolvedIn(aliases, table);

        await FillMetricGaps(resolved, table, version, cancellationToken);
        await RebuildFolderBaselines(catalogCharts, context, cancellationToken);
    }

    public async Task Consume(ConsumeContext<ImportPiuCenterSnapshotCommand> context)
    {
        var cancellationToken = context.CancellationToken;
        var now = _clock.Now;
        using var archive = new ZipArchive(new MemoryStream(context.Message.SnapshotZip), ZipArchiveMode.Read);

        string? ReadEntry(string name)
        {
            var entry = archive.GetEntry(name);
            if (entry == null) return null;
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        }

        var version = decimal.Parse((ReadEntry("version.txt") ??
                                     throw new InvalidOperationException(
                                         "snapshot zip carries no version.txt")).Trim());
        var table = PiuCenterDataParser.ParseChartTable(ReadEntry("page-content/chart-table.json") ??
                                                        throw new InvalidOperationException(
                                                            "snapshot zip carries no page-content/chart-table.json"));
        var listingByKey = table.ToDictionary(t => t.ExternalKey);
        var practiceByKey = PiuCenterDataParser
            .ParsePracticeLists(ReadEntry("page-content/stepchart-skills.json") ?? "[]")
            .GroupBy(e => e.ExternalKey)
            .ToDictionary(g => g.Key, g => g.ToArray());
        var predictions =
            PiuCenterDataParser.ParseDifficultyPredictions(ReadEntry("page-content/tierlists.json") ?? "{}");

        _logger.LogInformation("piucenter snapshot import: release {Version}, {Count} listed charts",
            version, table.Count);
        var catalogCharts = (await _charts.GetCharts(MatchCatalog, cancellationToken: cancellationToken))
            .ToArray();
        var aliases = await ReconcileAliases(table, catalogCharts, now, cancellationToken);
        var resolved = ResolvedIn(aliases, table);

        var banked = 0;
        foreach (var alias in resolved)
        {
            var body = ReadEntry($"{alias.ExternalKey}.json");
            if (body == null) continue;
            var page = PiuCenterDataParser.ParseChartPage(alias.ExternalKey, body);
            if (page == null) continue;
            var rows = BuildMetrics(alias.ChartId!.Value, version, page,
                listingByKey.GetValueOrDefault(alias.ExternalKey),
                practiceByKey.GetValueOrDefault(alias.ExternalKey),
                predictions.TryGetValue(alias.ExternalKey, out var prediction) ? prediction : null);
            await _metrics.ReplaceChartMetrics(alias.ChartId.Value, PiuCenterMetrics.Source, rows,
                cancellationToken);
            banked++;
        }

        _logger.LogInformation("piucenter snapshot import: banked metrics for {Banked}/{Total} resolved charts",
            banked, resolved.Length);
        await RebuildFolderBaselines(catalogCharts, context, cancellationToken);
    }

    private static ExternalChartAlias[] ResolvedIn(IReadOnlyList<ExternalChartAlias> aliases,
        IReadOnlyList<PiuCenterChartListing> table)
    {
        var listedKeys = table.Select(t => t.ExternalKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return aliases
            // A few keys name a stepchart that is not the chart we would hang it on — XX-era
            // branching paths where only one branch survives. Dropped here rather than at match
            // time, so a rejected key can never bank metrics over a good one.
            .Where(a => !PiuCenterKeyParser.IsRejected(a.ExternalKey))
            .Where(a => a.ChartId != null && a.Status != ExternalAliasStatus.NotFound)
            .Where(a => listedKeys.Contains(a.ExternalKey))
            .GroupBy(a => a.ExternalKey, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .ToArray();
    }

    private async Task<IReadOnlyList<ExternalChartAlias>> ReconcileAliases(
        IReadOnlyList<PiuCenterChartListing> table, IReadOnlyList<Chart> catalogCharts, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Ordinal-ignore-case throughout the alias path: the SQL unique index compares
        // case-insensitively, and piucenter's table carries case-twin junk rows
        // ("..._s19_ARCADE" vs "..._S19_ARCADE") that must not read as new keys.
        var known = (await _aliases.GetAliases(PiuCenterMetrics.Source, cancellationToken))
            .ToDictionary(a => a.ExternalKey, StringComparer.OrdinalIgnoreCase);
        var matchIndex = BuildMatchIndex(catalogCharts);
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

    /// <summary>
    ///     Recomputes every folder's per-badge baselines from the banked metrics
    ///     (docs/design/chart-identity.md §5). Runs at the end of an ingestion because that is
    ///     the only thing that moves the inputs, and it recomputes whole rather than in place:
    ///     a folder that changed shape has to lose its old rows.
    ///     <para>
    ///         Every mix is rebuilt, not just the crawled one. Metrics describe the steps and the
    ///         steps do not change between mixes — but the LEVEL does, so the same chart is
    ///         measured against different company in each catalog and each needs its own
    ///         baseline (the same-chart-different-level trap that bites every cross-mix read).
    ///     </para>
    /// </summary>
    private async Task RebuildFolderBaselines(IReadOnlyList<Chart> catalogCharts,
        IPublishEndpoint publisher, CancellationToken cancellationToken)
    {
        var metricsByChart = await _metrics.GetMetricsByChart(PiuCenterMetrics.Source, cancellationToken);
        if (metricsByChart.Count == 0)
        {
            _logger.LogInformation("piucenter: no banked metrics, folder baselines left alone");
            return;
        }

        var profiles = metricsByChart.ToDictionary(kv => kv.Key, kv => ChartBadgeProfile.From(kv.Key, kv.Value));
        // Every chart carrying metrics was matched against the Phoenix catalog, so that is
        // where its type comes from; the flat ChartMix read then places it in every other
        // mix that carries it.
        var typeById = catalogCharts.ToDictionary(c => c.Id, c => c.Type);

        var folders = new Dictionary<(MixEnum Mix, ChartType Type, int Level), List<ChartBadgeProfile>>();
        foreach (var (chartId, mix, level) in await _charts.GetChartMixLevels(cancellationToken))
        {
            if (!profiles.TryGetValue(chartId, out var profile)) continue;
            if (!typeById.TryGetValue(chartId, out var type)) continue;
            var key = (mix, type, level);
            if (!folders.TryGetValue(key, out var members)) folders[key] = members = new List<ChartBadgeProfile>();
            members.Add(profile);
        }

        // A folder is judged against its NEIGHBOURS, not only itself: levels L-1, L and L+1 of
        // the same type and mix. Charts do not respect level boundaries — a technique that is
        // ordinary at 21 is ordinary at 20 and 22 — and a single level is a thin sample to read
        // a percentile off, thinner still in the sparse folders at either end of the scale.
        // Piucenter reached the same shape independently (piu-annotate, get_top_chart_skills)
        // with a two-level window; the owner asked for symmetric.
        var byMix = folders
            .GroupBy(kv => kv.Key.Mix)
            .ToDictionary(g => g.Key, g => g
                .SelectMany(kv => FolderBaselineBuilder.Build(kv.Key.Mix, kv.Key.Type, kv.Key.Level,
                    PeersOf(folders, kv.Key)))
                .ToArray());

        foreach (var (mix, baselines) in byMix)
            await _baselines.ReplaceBaselines(mix, baselines, cancellationToken);

        _logger.LogInformation(
            "piucenter: rebuilt folder baselines — {Rows} rows across {Folders} folders in {Mixes} mixes",
            byMix.Values.Sum(b => b.Length), folders.Count, byMix.Count);

        await publisher.Publish(new PiuCenterDataIngestedEvent(byMix.Keys.ToArray()), cancellationToken);
    }

    /// <summary>
    ///     The charts a folder's percentiles are measured against: its own level and the two
    ///     either side, same mix and type. The folder's cutoffs still belong to ITS level — only
    ///     the population they are read from widens.
    /// </summary>
    private static IReadOnlyCollection<ChartBadgeProfile> PeersOf(
        IReadOnlyDictionary<(MixEnum Mix, ChartType Type, int Level), List<ChartBadgeProfile>> folders,
        (MixEnum Mix, ChartType Type, int Level) key)
    {
        var peers = new List<ChartBadgeProfile>();
        for (var level = key.Level - 1; level <= key.Level + 1; level++)
            if (folders.TryGetValue((key.Mix, key.Type, level), out var members))
                peers.AddRange(members);
        return peers;
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

        // A page whose note arrays parsed empty on both sides carries no shape worth
        // banking -- and a zero tap count it did not earn would poison the hold-tick
        // subtraction downstream. All-hold charts pass this gate on their hold rows.
        if (page.TapRows > 0 || page.HoldRows > 0)
        {
            rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.TapRows, page.TapRows, null));
            rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.HoldRows, page.HoldRows, null));
            rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.HoldTicks, page.HoldTickSum, null));
        }

        for (var i = 0; i < page.SkillSummary.Count; i++)
            rows.Add(new ChartSkillMetric(chartId, $"{PiuCenterMetrics.Top3Prefix}{page.SkillSummary[i]}", i + 1,
                null));

        if (page.SegmentCount > 0)
            foreach (var (skill, count) in page.SegmentSkillCounts)
                rows.Add(new ChartSkillMetric(chartId, $"{PiuCenterMetrics.BadgeFractionPrefix}{skill}",
                    Math.Round((decimal)count / page.SegmentCount, 4), null));

        foreach (var skill in page.LastSegmentSkills)
            rows.Add(new ChartSkillMetric(chartId, $"{PiuCenterMetrics.LastSegmentPrefix}{skill}", 1, null));
        rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.LastSegmentIsPeak,
            page.LastSegmentIsPeak ? 1 : 0, null));

        // The chart at its hardest (docs/design/chart-identity.md §4). Peakiness is the one
        // piece that needs the printed level, so it banks only when the page carried one.
        if (page.Crux is { } crux)
        {
            rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.CruxLevel, crux.Level, null));
            if (crux.Peakiness != null)
                rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.CruxPeakiness, crux.Peakiness.Value, null));
            rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.CruxPosition, crux.Position, null));
            rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.CruxDuration, crux.Duration, null));
            if (crux.Enps != null)
                rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.CruxEnps, crux.Enps.Value, null));
            for (var i = 0; i < crux.Badges.Count; i++)
                rows.Add(new ChartSkillMetric(chartId,
                    Truncate($"{PiuCenterMetrics.CruxBadgePrefix}{crux.Badges[i]}", 64), i + 1, null));
        }

        // Where the body goes (docs/design/chart-identity.md §4b). Pad shares only mean anything
        // on doubles — a singles chart is trivially "all middle" and a width claim about one
        // would be noise — but the stance and bracket shares read the same on either.
        if (page.Stance is { } stance)
        {
            if (stance.IsDoubles)
            {
                rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.PadShareMid4, stance.PadShareMid4, null));
                rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.PadShareMid6, stance.PadShareMid6, null));
            }

            rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.StanceDiagonal, stance.Diagonal, null));
            rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.StanceSideOn, stance.SideOn, null));
            rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.StanceCrossed, stance.Crossed, null));
            rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.BracketRowShare, stance.BracketRowShare, null));
            rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.RepeatedPanelShare,
                stance.RepeatedPanelShare, null));
        }

        if (page.ChartSpanSeconds > 0)
        {
            rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.ChartSpan, page.ChartSpanSeconds, null));
        }

        // Provenance, so a chip built from a pre-Phoenix stepchart can be found later.
        if (page.Pack != null)
            rows.Add(new ChartSkillMetric(chartId, PiuCenterMetrics.PackIsPhoenix,
                page.Pack.Equals("PHOENIX", StringComparison.OrdinalIgnoreCase) ? 1 : 0, null));

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

    private static Dictionary<string, Guid?> BuildMatchIndex(IReadOnlyList<Chart> catalogCharts)
    {
        var index = new Dictionary<string, Guid?>();
        foreach (var chart in catalogCharts)
        {
            // ChartType.HalfDouble (#138) is a retired legacy-era chart type and never
            // appears in the Phoenix set this index is built from — piucenter's
            // HALFDOUBLE_ key label refers to modern DOUBLE charts (TryMatch's relax).
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
                $"{PiuCenterKeyParser.Normalize(chart.Song.Name)}|{PiuCenterKeyParser.Normalize(chart.Song.Artist)}|{sord}|{(int)chart.Level}|{variant}";
            // Ambiguous keys match nothing — better parked than misbound.
            index[key] = index.ContainsKey(key) ? null : chart.Id;
        }

        return index;
    }

    private static Guid? TryMatch(IReadOnlyDictionary<string, Guid?> matchIndex, PiuCenterChartListing listing)
    {
        if (!PiuCenterKeyParser.TryParse(listing.ExternalKey, out var parts)) return null;
        var sord = listing.Type == ChartType.Single ? "singles" : "doubles";
        // piucenter labels half-double-style charts with a HALFDOUBLE_ key prefix, but
        // in the modern catalog those ARE Double charts — strip and match Double
        // (same as the seeding pass's tier 3a; owner-confirmed).
        var variant = listing.Variant.StartsWith("HALFDOUBLE_", StringComparison.Ordinal)
            ? listing.Variant["HALFDOUBLE_".Length..]
            : listing.Variant;
        var key =
            $"{PiuCenterKeyParser.Normalize(parts.SongPart)}|{PiuCenterKeyParser.Normalize(parts.ArtistPart)}|{sord}|{listing.Level}|{variant}";
        return matchIndex.GetValueOrDefault(key);
    }
}
