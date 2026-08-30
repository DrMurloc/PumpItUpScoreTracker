using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Catalog.Application;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class StepChartIngestTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid ChartId = Guid.NewGuid();

    private const string Ssc = """
        #OFFSET:0;
        #BPMS:0=120;
        #NOTEDATA:;
        #STEPSTYPE:pump-single;
        #METER:21;
        #NOTES:
        10000
        01000
        00100
        00010
        ;
        """;

    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<IChartStepChartRepository> _steps = new();
    private readonly Mock<IStepFileStore> _store = new();

    public StepChartIngestTests()
    {
        _charts.Setup(c => c.GetChartMixLevels(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                (ChartId, MixEnum.Phoenix, 21, (int?)4),
                (ChartId, MixEnum.Phoenix2, 21, (int?)null)
            });
    }

    private StepChartIngest Build()
    {
        return new StepChartIngest(_steps.Object, _store.Object, _charts.Object,
            FakeDateTime.At(Now).Object, NullLogger<StepChartIngest>.Instance);
    }

    private static ZipArchive Zip(params (string Name, string Content)[] entries)
    {
        var buffer = new MemoryStream();
        using (var writer = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            foreach (var (name, content) in entries)
            {
                using var stream = writer.CreateEntry(name).Open();
                stream.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        buffer.Position = 0;
        return new ZipArchive(buffer, ZipArchiveMode.Read);
    }

    private static PiuCenterChartSteps Steps(string? sscFile = "C:/x/PIU-Simfiles/16 - PHOENIX\\A - Song\\Song.ssc")
    {
        return new PiuCenterChartSteps(
            new[]
            {
                new StepArrow(0, 0.0m, "l"),
                new StepArrow(1, 0.5m, "r"),
                new StepArrow(2, 1.0m, "l"),
                new StepArrow(3, 1.5m, "r")
            },
            Array.Empty<PiuCenterStepHold>(),
            Array.Empty<PiuCenterTickSpan>(),
            new[] { new PiuCenterSegmentSpan(0m, 2m, 4.0m) },
            new[] { new PiuCenterRangeSpan(0.5m, 1.5m) },
            sscFile, "pump-single", 21);
    }

    private Dictionary<Guid, BankedStepChart> CaptureBanked()
    {
        var captured = new Dictionary<Guid, BankedStepChart>();
        _steps.Setup(s => s.Replace(It.IsAny<IReadOnlyDictionary<Guid, BankedStepChart>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyDictionary<Guid, BankedStepChart>, CancellationToken>((banked, _) =>
            {
                foreach (var (id, row) in banked) captured[id] = row;
            })
            .Returns(Task.CompletedTask);
        return captured;
    }

    [Fact]
    public async Task BanksAnAlignedPayloadWithBorrowedPhoenix2Counts()
    {
        var captured = CaptureBanked();
        using var zip = Zip(("stepfiles/16 - PHOENIX/A - Song/Song.ssc", Ssc));

        await Build().Bank(zip, "82626",
            new Dictionary<Guid, PiuCenterChartSteps> { [ChartId] = Steps() }, CancellationToken.None);

        var banked = Assert.Contains(ChartId, (IDictionary<Guid, BankedStepChart>)captured);
        Assert.Equal("82626", banked.Vintage);
        var payload = StepChartPayloadCodec.Decode(banked.Payload);
        Assert.NotNull(payload);
        Assert.True(payload!.Aligned);
        Assert.Equal(new decimal?[] { 0m, 1m, 2m, 3m }, payload.Rows.Select(r => r.B));
        // Phoenix judged 4 = implied 4: Full. Phoenix 2 borrowed Phoenix's count: Full too.
        Assert.Equal((int)StepChartVisibility.Full, payload.Verdicts["Phoenix"].V);
        Assert.Equal((int)StepChartVisibility.Full, payload.Verdicts["Phoenix2"].V);
        Assert.Equal("16 - PHOENIX/A - Song/Song.ssc", payload.Ssc);
        Assert.Equal(1, Assert.Single(payload.Ranges).E - Assert.Single(payload.Ranges).S);
    }

    [Fact]
    public async Task ArchivesTheStepTreeOnlyWhenAStoreIsConfigured()
    {
        CaptureBanked();
        _store.SetupGet(s => s.IsConfigured).Returns(true);
        using var zip = Zip(("stepfiles/16 - PHOENIX/A - Song/Song.ssc", Ssc));

        await Build().Bank(zip, "82626",
            new Dictionary<Guid, PiuCenterChartSteps> { [ChartId] = Steps() }, CancellationToken.None);

        _store.Verify(s => s.Put("82626", "16 - PHOENIX/A - Song/Song.ssc", It.IsAny<Stream>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AMissingStoreSkipsTheArchiveAndBanksAllTheSame()
    {
        var captured = CaptureBanked();
        _store.SetupGet(s => s.IsConfigured).Returns(false);
        using var zip = Zip(("stepfiles/16 - PHOENIX/A - Song/Song.ssc", Ssc));

        await Build().Bank(zip, "82626",
            new Dictionary<Guid, PiuCenterChartSteps> { [ChartId] = Steps() }, CancellationToken.None);

        Assert.Single(captured);
        _store.Verify(s => s.Put(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AMissingSscStillBanksSecondsOnly()
    {
        var captured = CaptureBanked();
        using var zip = Zip(("stepfiles/somewhere/else.ssc", Ssc));

        await Build().Bank(zip, "82626",
            new Dictionary<Guid, PiuCenterChartSteps> { [ChartId] = Steps() }, CancellationToken.None);

        var payload = StepChartPayloadCodec.Decode(captured[ChartId].Payload)!;
        Assert.False(payload.Aligned);
        Assert.All(payload.Rows, r => Assert.Null(r.B));
    }

    [Fact]
    public async Task NothingToBankNeverTouchesTheRepository()
    {
        using var zip = Zip();

        await Build().Bank(zip, "82626", new Dictionary<Guid, PiuCenterChartSteps>(), CancellationToken.None);

        _steps.Verify(s => s.Replace(It.IsAny<IReadOnlyDictionary<Guid, BankedStepChart>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("C:/Users/x/repos/PIU-Simfiles/16 - PHOENIX\\A\\S.ssc", "16 - PHOENIX/A/S.ssc")]
    [InlineData("/mnt/data/PIU-Simfiles/02 - S.E.~EXTRA/B/T.ssc", "02 - S.E.~EXTRA/B/T.ssc")]
    [InlineData("C:/elsewhere/steps/S.ssc", null)]
    [InlineData(null, null)]
    public void TheGeneratorsPathCutsDownToTheCheckoutRelativeOne(string? sscFile, string? expected)
    {
        Assert.Equal(expected, StepChartIngest.RelativeSscPath(sscFile));
    }
}
