using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Catalog.Application;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Messages;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class StepChartReprocessConsumerTests
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
        ;
        """;

    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<IChartStepChartRepository> _steps = new();
    private readonly Mock<IStepFileStore> _store = new();

    private StepChartReprocessConsumer Build()
    {
        return new StepChartReprocessConsumer(_steps.Object, _store.Object, _charts.Object,
            FakeDateTime.At(Now).Object, NullLogger<StepChartReprocessConsumer>.Instance);
    }

    private Task Consume()
    {
        var context = new Mock<ConsumeContext<ReprocessStepFilesCommand>>();
        context.SetupGet(c => c.Message).Returns(new ReprocessStepFilesCommand());
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return Build().Consume(context.Object);
    }

    private static byte[] SecondsOnlyPayload()
    {
        // Two rows one measure apart: the ssc's two half-note lines land at 0 s and 1 s.
        var rows = new List<EnrichedRow>
        {
            new(0.0m) { PanelMask = 1 },
            new(1.0m) { PanelMask = 2 }
        };
        var enriched = new EnrichedStepChart(5, false, rows,
            Array.Empty<SnapshotHold>(), Array.Empty<decimal>(),
            Array.Empty<SnapshotSegment>(), Array.Empty<SnapshotRange>(),
            new Dictionary<MixEnum, StepChartVerdict>
            {
                [MixEnum.Phoenix] = new(StepChartVisibility.StepsOnly, null, 2)
            },
            2, 0, "16 - PHOENIX/A/Song.ssc", "pump-single", 21);
        return StepChartPayloadCodec.Encode(enriched);
    }

    [Fact]
    public async Task RefreshesFromTheNewestArchivedVintage()
    {
        _store.SetupGet(s => s.IsConfigured).Returns(true);
        _store.Setup(s => s.ListVintages(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "50726", "82626" });
        _store.Setup(s => s.GetText("82626", "16 - PHOENIX/A/Song.ssc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ssc);
        _steps.Setup(s => s.GetBankedChartIds(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ChartId });
        _steps.Setup(s => s.Get(ChartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankedStepChart("50726", Now.AddDays(-30), SecondsOnlyPayload()));
        // The judged count arrived since the original ingest: the verdict must move to Full.
        _charts.Setup(c => c.GetChartMixLevels(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { (ChartId, MixEnum.Phoenix, 21, (int?)2) });
        IReadOnlyDictionary<Guid, BankedStepChart>? captured = null;
        _steps.Setup(s => s.Replace(It.IsAny<IReadOnlyDictionary<Guid, BankedStepChart>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyDictionary<Guid, BankedStepChart>, CancellationToken>((banked, _) =>
                captured = banked)
            .Returns(Task.CompletedTask);

        await Consume();

        Assert.NotNull(captured);
        var banked = captured![ChartId];
        Assert.Equal("82626", banked.Vintage);
        var payload = StepChartPayloadCodec.Decode(banked.Payload)!;
        Assert.True(payload.Aligned);
        Assert.Equal(new decimal?[] { 0m, 2m }, payload.Rows.Select(r => r.B));
        Assert.Equal((int)StepChartVisibility.Full, payload.Verdicts["Phoenix"].V);
    }

    [Fact]
    public async Task AnUnconfiguredStoreLeavesEverythingAlone()
    {
        _store.SetupGet(s => s.IsConfigured).Returns(false);

        await Consume();

        _steps.Verify(s => s.Replace(It.IsAny<IReadOnlyDictionary<Guid, BankedStepChart>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnEmptyArchiveLeavesEverythingAlone()
    {
        _store.SetupGet(s => s.IsConfigured).Returns(true);
        _store.Setup(s => s.ListVintages(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        await Consume();

        _steps.Verify(s => s.Replace(It.IsAny<IReadOnlyDictionary<Guid, BankedStepChart>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
