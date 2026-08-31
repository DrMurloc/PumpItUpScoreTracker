using System.IO.Compression;
using System.Text.Json;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     The banked payload's wire form: the enriched chart serialized to terse JSON and
///     gzipped. Version-stamped so a future shape change can read old rows instead of
///     mistrusting them; property names are single letters because a boss chart carries
///     thousands of rows and the row keys are most of the raw bytes.
/// </summary>
internal static class StepChartPayloadCodec
{
    // 2: segments carry piucenter's per-passage skill badges and model level (owner,
    // 2026-08-31). Decode never checks the stamp — v1 rows read back with null badge/level
    // fields, which the section-labeling chips treat as "unlabeled", never as an error.
    private const int Version = 2;

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static byte[] Encode(EnrichedStepChart chart)
    {
        var payload = new StepChartPayload(
            Version,
            chart.Panels,
            chart.BeatsAligned,
            chart.TapRowCount,
            chart.TickSum,
            chart.Rows.Select(r => new PayloadRow(r.Time, r.PanelMask, r.LeftMask, r.Quant, r.Beat)).ToArray(),
            chart.Holds.Select(h => new PayloadHold(h.Panel, h.Start, h.End, h.Limb == "l")).ToArray(),
            chart.TickTimes,
            chart.Segments.Select(s => new PayloadSegment(s.Start, s.End, s.Enps,
                s.Badges is { Count: > 0 } ? s.Badges : null, s.Level, s.Pace)).ToArray(),
            chart.RangesOfInterest.Select(r => new PayloadRange(r.Start, r.End)).ToArray(),
            chart.Verdicts.ToDictionary(
                kv => kv.Key.ToString(),
                kv => new PayloadVerdict((int)kv.Value.Visibility, kv.Value.NoteCount, kv.Value.ImpliedTotal)),
            chart.SscFile,
            chart.StepsType,
            chart.Meter);

        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, true))
        {
            JsonSerializer.Serialize(gzip, payload, Options);
        }

        return buffer.ToArray();
    }

    /// <summary>Null on any malformed row — a broken payload reads as "no step chart", never a throw.</summary>
    public static StepChartPayload? Decode(byte[] payload)
    {
        try
        {
            using var buffer = new MemoryStream(payload);
            using var gzip = new GZipStream(buffer, CompressionMode.Decompress);
            return JsonSerializer.Deserialize<StepChartPayload>(gzip, Options);
        }
        catch (Exception e) when (e is JsonException or InvalidDataException)
        {
            return null;
        }
    }

    public static StepChartVerdict? VerdictFor(StepChartPayload payload, MixEnum mix)
    {
        return payload.Verdicts.TryGetValue(mix.ToString(), out var verdict)
            ? new StepChartVerdict((Contracts.StepChartVisibility)verdict.V, verdict.N, verdict.I)
            : null;
    }
}

internal sealed record StepChartPayload(
    int V,
    int Panels,
    bool Aligned,
    int TapRows,
    int TickSum,
    IReadOnlyList<PayloadRow> Rows,
    IReadOnlyList<PayloadHold> Holds,
    IReadOnlyList<decimal> Ticks,
    IReadOnlyList<PayloadSegment> Segments,
    IReadOnlyList<PayloadRange> Ranges,
    IReadOnlyDictionary<string, PayloadVerdict> Verdicts,
    string? Ssc = null,
    string? StepsType = null,
    int? Meter = null);

internal sealed record PayloadRow(decimal T, int M, int L, int Q, decimal? B);

internal sealed record PayloadHold(int P, decimal S, decimal E, bool L);

internal sealed record PayloadSegment(
    decimal S,
    decimal E,
    decimal? N,
    IReadOnlyList<string>? B = null,
    decimal? L = null,
    string? P = null);

internal sealed record PayloadRange(decimal S, decimal E);

internal sealed record PayloadVerdict(int V, int? N, int I);
