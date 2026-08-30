using System.IO.Compression;
using Microsoft.Extensions.Logging;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Application;

/// <summary>
///     The snapshot import's step-chart half (docs/design/step-chart-failure-map.md D4–D7):
///     for every resolved chart whose page carried raw steps, find its .ssc inside the zip's
///     <c>stepfiles/</c> tree (the generator's own <c>ssc_file</c> path names it), align,
///     enrich, and bank one payload per chart — then archive the raw tree to blob when a store
///     is configured. The parse path reads the zip directly, so the archive is a side-write
///     and a secret-less environment banks everything all the same.
/// </summary>
internal sealed class StepChartIngest
{
    private const string StepFilesPrefix = "stepfiles/";

    /// <summary>
    ///     The corpus-root marker inside the generator's <c>ssc_file</c> paths. "simfiles/"
    ///     rather than "PIU-Simfiles/": the canonical corpus moved to
    ///     PiuScoresStepfiles/<c>simfiles/</c> (its README is the loop's spec), and the old
    ///     checkout's "PIU-Simfiles/" still ends in the same marker — one split serves every
    ///     vintage ever generated.
    /// </summary>
    private const string CorpusRootMarker = "simfiles/";

    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly ILogger<StepChartIngest> _logger;
    private readonly IChartStepChartRepository _steps;
    private readonly IStepFileStore _store;

    public StepChartIngest(IChartStepChartRepository steps, IStepFileStore store, IChartRepository charts,
        IDateTimeOffsetAccessor clock, ILogger<StepChartIngest> logger)
    {
        _steps = steps;
        _store = store;
        _charts = charts;
        _clock = clock;
        _logger = logger;
    }

    public async Task Bank(ZipArchive archive, string vintage,
        IReadOnlyDictionary<Guid, PiuCenterChartSteps> stepsByChart, CancellationToken cancellationToken)
    {
        if (stepsByChart.Count == 0)
        {
            _logger.LogInformation("step charts: the snapshot carried no step content — nothing banked");
            return;
        }

        var noteCounts = await NoteCountsByChart(cancellationToken);
        var entriesByName = archive.Entries
            .Where(e => e.FullName.Replace('\\', '/').StartsWith(StepFilesPrefix, StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => e.FullName.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var documents = new Dictionary<string, StepFileDocument?>(StringComparer.OrdinalIgnoreCase);

        var banked = new Dictionary<Guid, BankedStepChart>();
        var aligned = 0;
        var withSsc = 0;
        var now = _clock.Now;
        foreach (var (chartId, steps) in stepsByChart)
        {
            var relative = RelativeSscPath(steps.SscFile);
            var ssc = relative == null
                ? null
                : SelectSscChart(entriesByName, documents, relative, steps.StepsType, steps.Meter);
            if (ssc != null) withSsc++;

            var enriched = StepChartEnricher.Enrich(ToSnapshot(steps, relative), ssc,
                noteCounts.TryGetValue(chartId, out var counts)
                    ? counts
                    : new Dictionary<MixEnum, int?> { [MixEnum.Phoenix] = null, [MixEnum.Phoenix2] = null });
            if (enriched.BeatsAligned) aligned++;
            banked[chartId] = new BankedStepChart(vintage, now, StepChartPayloadCodec.Encode(enriched));
        }

        await _steps.Replace(banked, cancellationToken);
        _logger.LogInformation(
            "step charts: banked {Banked} timelines from release {Vintage} — {WithSsc} matched an .ssc, {Aligned} aligned to beats",
            banked.Count, vintage, withSsc, aligned);

        await Archive(entriesByName.Values, vintage, cancellationToken);
    }

    /// <summary>
    ///     The judged totals verdicts are measured against, per mix. Phoenix 2's counts are
    ///     still refilling from play, so a null there borrows Phoenix 1's — the same fallback
    ///     the folder-baseline rebuild applies, for the same reason: those two catalogs judge
    ///     the same steppings.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<MixEnum, int?>>> NoteCountsByChart(
        CancellationToken cancellationToken)
    {
        var mixLevels = await _charts.GetChartMixLevels(cancellationToken);
        var byChart = new Dictionary<Guid, Dictionary<MixEnum, int?>>();
        foreach (var (chartId, mix, _, noteCount) in mixLevels)
        {
            if (mix is not (MixEnum.Phoenix or MixEnum.Phoenix2)) continue;
            if (!byChart.TryGetValue(chartId, out var counts))
                byChart[chartId] = counts = new Dictionary<MixEnum, int?>
                {
                    [MixEnum.Phoenix] = null,
                    [MixEnum.Phoenix2] = null
                };
            counts[mix] = noteCount;
        }

        foreach (var counts in byChart.Values)
            counts[MixEnum.Phoenix2] ??= counts[MixEnum.Phoenix];

        return byChart.ToDictionary(kv => kv.Key,
            kv => (IReadOnlyDictionary<MixEnum, int?>)kv.Value);
    }

    /// <summary>
    ///     The generator's absolute path cut down to the corpus-relative one the zip mirrors:
    ///     ".../PIU-Simfiles/16 - PHOENIX\18230 - Altale\Altale.ssc" and
    ///     ".../PiuScoresStepfiles/simfiles/16 - PHOENIX/18230 - Altale/Altale.ssc" both become
    ///     "16 - PHOENIX/18230 - Altale/Altale.ssc".
    /// </summary>
    internal static string? RelativeSscPath(string? sscFile)
    {
        if (string.IsNullOrWhiteSpace(sscFile)) return null;
        var normalized = sscFile.Replace('\\', '/');
        var marker = normalized.LastIndexOf(CorpusRootMarker, StringComparison.OrdinalIgnoreCase);
        if (marker < 0) return null;
        var relative = normalized[(marker + CorpusRootMarker.Length)..].Trim('/');
        return relative.Length == 0 ? null : relative;
    }

    private StepChartData? SelectSscChart(IReadOnlyDictionary<string, ZipArchiveEntry> entriesByName,
        IDictionary<string, StepFileDocument?> documents, string relative, string? stepsType, int? meter)
    {
        if (stepsType == null || meter == null) return null;
        var entryName = StepFilesPrefix + relative;
        if (!documents.TryGetValue(entryName, out var document))
        {
            documents[entryName] = document = ReadDocument(entriesByName, entryName);
            if (document == null)
                _logger.LogInformation("step charts: no {Entry} in the upload — seconds only", entryName);
        }

        if (document == null) return null;
        var chart = StepFileParser.SelectChart(document, stepsType, meter.Value);
        return chart == null ? null : StepChartTimeline.Build(document, chart);
    }

    private static StepFileDocument? ReadDocument(IReadOnlyDictionary<string, ZipArchiveEntry> entriesByName,
        string entryName)
    {
        if (!entriesByName.TryGetValue(entryName, out var entry)) return null;
        using var reader = new StreamReader(entry.Open());
        return StepFileParser.Parse(reader.ReadToEnd());
    }

    private static SnapshotStepData ToSnapshot(PiuCenterChartSteps steps, string? relativeSsc)
    {
        return new SnapshotStepData(
            steps.Taps.Select(t => new SnapshotArrow(t.Panel, t.Time, t.Limb)).ToArray(),
            steps.Holds.Select(h => new SnapshotHold(h.Panel, h.Start, h.End, h.Limb)).ToArray(),
            steps.TickSpans.Select(t => new SnapshotTickSpan(t.Start, t.End, t.Count)).ToArray(),
            steps.Segments.Select(s => new SnapshotSegment(s.Start, s.End, s.Enps)).ToArray(),
            steps.RangesOfInterest.Select(r => new SnapshotRange(r.Start, r.End)).ToArray(),
            relativeSsc, steps.StepsType, steps.Meter);
    }

    /// <summary>Custody, never the read path: a failed or skipped archive costs the copy, not the feature.</summary>
    private async Task Archive(IEnumerable<ZipArchiveEntry> entries, string vintage,
        CancellationToken cancellationToken)
    {
        if (!_store.IsConfigured)
        {
            _logger.LogInformation(
                "step charts: no step-file store configured — the raw corpus was not archived (design doc D7)");
            return;
        }

        var archived = 0;
        foreach (var entry in entries)
        {
            if (entry.Length == 0) continue;
            try
            {
                await using var stream = entry.Open();
                await _store.Put(vintage, entry.FullName.Replace('\\', '/')[StepFilesPrefix.Length..], stream,
                    cancellationToken);
                archived++;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _logger.LogWarning(e, "step charts: archiving {Entry} failed — continuing", entry.FullName);
            }
        }

        _logger.LogInformation("step charts: archived {Count} step files under vintage {Vintage}", archived,
            vintage);
    }
}
