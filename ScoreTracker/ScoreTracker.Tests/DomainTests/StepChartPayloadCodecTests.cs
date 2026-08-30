using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public class StepChartPayloadCodecTests
{
    private static EnrichedStepChart Sample()
    {
        var rows = new List<EnrichedRow>
        {
            new(1.25m) { PanelMask = (1 << 0) | (1 << 4), LeftMask = 1 << 0, Beat = 2m, Quant = 4 },
            new(1.5m) { PanelMask = 1 << 2, LeftMask = 0, Beat = 2.5m, Quant = 8 }
        };
        return new EnrichedStepChart(5, true, rows,
            new[] { new SnapshotHold(2, 3m, 4.5m, "l") },
            new[] { 3.1m, 3.6m },
            new[] { new SnapshotSegment(0m, 10m, 4.2m, new[] { "drill", "run" }, 19.92m) },
            new[] { new SnapshotRange(5m, 8m) },
            new Dictionary<MixEnum, StepChartVerdict>
            {
                [MixEnum.Phoenix] = new(StepChartVisibility.Full, 1299, 1313),
                [MixEnum.Phoenix2] = new(StepChartVisibility.StepsOnly, 1400, 1313)
            },
            2, 2);
    }

    [Fact]
    public void RoundTripsEveryField()
    {
        var decoded = StepChartPayloadCodec.Decode(StepChartPayloadCodec.Encode(Sample()));

        Assert.NotNull(decoded);
        Assert.Equal(5, decoded!.Panels);
        Assert.True(decoded.Aligned);
        Assert.Equal(2, decoded.TapRows);
        Assert.Equal(2, decoded.TickSum);
        Assert.Equal(2, decoded.Rows.Count);
        Assert.Equal(1.25m, decoded.Rows[0].T);
        Assert.Equal((1 << 0) | (1 << 4), decoded.Rows[0].M);
        Assert.Equal(1 << 0, decoded.Rows[0].L);
        Assert.Equal(4, decoded.Rows[0].Q);
        Assert.Equal(2m, decoded.Rows[0].B);
        var hold = Assert.Single(decoded.Holds);
        Assert.Equal(2, hold.P);
        Assert.True(hold.L);
        Assert.Equal(new[] { 3.1m, 3.6m }, decoded.Ticks);
        var segment = Assert.Single(decoded.Segments);
        Assert.Equal(4.2m, segment.N);
        Assert.Equal(new[] { "drill", "run" }, segment.B);
        Assert.Equal(19.92m, segment.L);
        Assert.Equal(5m, Assert.Single(decoded.Ranges).S);
    }

    [Fact]
    public void AVersionOnePayloadDecodesWithUnlabeledSegments()
    {
        // Rows banked before the section-labeling round carry no badge or level fields; they
        // must read back as "unlabeled", never as an error — the whole bank predates v2 until
        // the owner's next combined-zip upload.
        var v1 = """
                 {"V":1,"Panels":5,"Aligned":false,"TapRows":1,"TickSum":0,
                  "Rows":[{"T":1.0,"M":1,"L":0,"Q":0,"B":null}],"Holds":[],"Ticks":[],
                  "Segments":[{"S":0,"E":10,"N":4.2}],"Ranges":[],
                  "Verdicts":{"Phoenix":{"V":1,"N":10,"I":10}}}
                 """;
        using var buffer = new System.IO.MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(buffer,
                   System.IO.Compression.CompressionLevel.Fastest, true))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(v1);
            gzip.Write(bytes, 0, bytes.Length);
        }

        var decoded = StepChartPayloadCodec.Decode(buffer.ToArray());

        Assert.NotNull(decoded);
        var segment = Assert.Single(decoded!.Segments);
        Assert.Equal(4.2m, segment.N);
        Assert.Null(segment.B);
        Assert.Null(segment.L);
    }

    [Fact]
    public void VerdictsSurvivePerMix()
    {
        var decoded = StepChartPayloadCodec.Decode(StepChartPayloadCodec.Encode(Sample()))!;

        var phoenix = StepChartPayloadCodec.VerdictFor(decoded, MixEnum.Phoenix);
        var phoenix2 = StepChartPayloadCodec.VerdictFor(decoded, MixEnum.Phoenix2);

        Assert.Equal(StepChartVisibility.Full, phoenix!.Visibility);
        Assert.Equal(1299, phoenix.NoteCount);
        Assert.Equal(StepChartVisibility.StepsOnly, phoenix2!.Visibility);
        Assert.Null(StepChartPayloadCodec.VerdictFor(decoded, MixEnum.XX));
    }

    [Fact]
    public void AnUnalignedChartOmitsItsBeats()
    {
        var chart = Sample() with
        {
            BeatsAligned = false,
            Rows = Sample().Rows.Select(r => new EnrichedRow(r.Time) { PanelMask = r.PanelMask }).ToArray()
        };

        var decoded = StepChartPayloadCodec.Decode(StepChartPayloadCodec.Encode(chart))!;

        Assert.False(decoded.Aligned);
        Assert.All(decoded.Rows, r => Assert.Null(r.B));
    }

    [Fact]
    public void GarbageDecodesToNothingRatherThanAThrow()
    {
        Assert.Null(StepChartPayloadCodec.Decode(new byte[] { 1, 2, 3 }));
        Assert.Null(StepChartPayloadCodec.Decode(Array.Empty<byte>()));
    }
}
