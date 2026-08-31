using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.Catalog.Contracts.Messages;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Application;

/// <summary>
///     The reprocess button's consumer (docs/design/step-chart-failure-map.md D7): re-run the
///     .ssc-dependent half of enrichment for every banked chart from the newest archived
///     vintage — repaired step files and refilled note counts take effect without an upload.
///     Inert with a log line when no store is configured or nothing is archived.
/// </summary>
internal sealed class StepChartReprocessConsumer : IConsumer<ReprocessStepFilesCommand>
{
    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly ILogger<StepChartReprocessConsumer> _logger;
    private readonly IChartStepChartRepository _steps;
    private readonly IStepFileStore _store;

    public StepChartReprocessConsumer(IChartStepChartRepository steps, IStepFileStore store,
        IChartRepository charts, IDateTimeOffsetAccessor clock, ILogger<StepChartReprocessConsumer> logger)
    {
        _steps = steps;
        _store = store;
        _charts = charts;
        _clock = clock;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReprocessStepFilesCommand> context)
    {
        var cancellationToken = context.CancellationToken;
        if (!_store.IsConfigured)
        {
            _logger.LogInformation("step charts: reprocess asked with no store configured — nothing to read");
            return;
        }

        var vintages = await _store.ListVintages(cancellationToken);
        if (vintages.Count == 0)
        {
            _logger.LogInformation("step charts: reprocess asked with an empty archive — nothing to read");
            return;
        }

        // Vintages are numeric release stamps; the newest is the largest. A stray non-numeric
        // folder sorts last lexically rather than throwing.
        var vintage = vintages
            .OrderBy(v => decimal.TryParse(v, out var stamp) ? stamp : decimal.MinValue)
            .ThenBy(v => v, StringComparer.Ordinal)
            .Last();

        var chartIds = await _steps.GetBankedChartIds(cancellationToken);
        var mixLevels = await _charts.GetChartMixLevels(cancellationToken);
        var noteCounts = new Dictionary<Guid, Dictionary<MixEnum, int?>>();
        foreach (var (chartId, mix, _, noteCount) in mixLevels)
        {
            if (mix is not (MixEnum.Phoenix or MixEnum.Phoenix2)) continue;
            if (!noteCounts.TryGetValue(chartId, out var counts))
                noteCounts[chartId] = counts = new Dictionary<MixEnum, int?>
                {
                    [MixEnum.Phoenix] = null,
                    [MixEnum.Phoenix2] = null
                };
            counts[mix] = noteCount;
        }

        foreach (var counts in noteCounts.Values)
            counts[MixEnum.Phoenix2] ??= counts[MixEnum.Phoenix];

        var documents = new Dictionary<string, StepFileDocument?>(StringComparer.OrdinalIgnoreCase);
        var refreshedByChart = new Dictionary<Guid, EnrichedStepChart>();
        var aligned = 0;
        var now = _clock.Now;
        foreach (var chartId in chartIds)
        {
            var existing = await _steps.Get(chartId, cancellationToken);
            if (existing == null) continue;
            var payload = StepChartPayloadCodec.Decode(existing.Payload);
            if (payload == null) continue;

            StepChartData? ssc = null;
            if (payload is { Ssc: not null, StepsType: not null, Meter: not null })
            {
                if (!documents.TryGetValue(payload.Ssc, out var document))
                {
                    var text = await _store.GetText(vintage, payload.Ssc, cancellationToken);
                    documents[payload.Ssc] = document = text == null ? null : StepFileParser.Parse(text);
                }

                if (document != null)
                {
                    var chart = StepFileParser.SelectChart(document, payload.StepsType, payload.Meter.Value);
                    if (chart != null) ssc = StepChartTimeline.Build(document, chart);
                }
            }

            var refreshed = StepChartReprocessor.Refresh(payload, ssc,
                noteCounts.TryGetValue(chartId, out var counts)
                    ? counts
                    : new Dictionary<MixEnum, int?> { [MixEnum.Phoenix] = null, [MixEnum.Phoenix2] = null });
            if (refreshed.BeatsAligned) aligned++;
            refreshedByChart[chartId] = refreshed;
        }

        // Pace re-stamps here too — the reprocess walks the whole bank, so the folder
        // distributions are complete without an upload.
        var banked = SegmentPaceClassifier.Stamp(refreshedByChart).ToDictionary(
            kv => kv.Key,
            kv => new BankedStepChart(vintage, now, StepChartPayloadCodec.Encode(kv.Value)));

        await _steps.Replace(banked, cancellationToken);
        _logger.LogInformation(
            "step charts: reprocessed {Count} timelines from archived vintage {Vintage} — {Aligned} aligned to beats",
            banked.Count, vintage, aligned);
    }
}
