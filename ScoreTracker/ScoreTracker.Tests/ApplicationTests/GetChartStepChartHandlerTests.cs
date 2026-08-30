using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Catalog.Application;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetChartStepChartHandlerTests
{
    private static readonly Guid ChartId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IChartStepChartRepository> _stepCharts = new();

    private GetChartStepChartHandler Build()
    {
        return new GetChartStepChartHandler(_stepCharts.Object);
    }

    private void SetupPayload(params (MixEnum Mix, StepChartVisibility Visibility)[] verdicts)
    {
        var rows = new List<EnrichedRow>
        {
            new(0.5m) { PanelMask = 1 | (1 << 4), LeftMask = 1, Beat = 1m, Quant = 4 }
        };
        var enriched = new EnrichedStepChart(5, true, rows,
            new[] { new SnapshotHold(2, 1m, 2m, "l") },
            new[] { 1.5m },
            new[] { new SnapshotSegment(0m, 3m, 5.5m, new[] { "bracket_run" }, 21.4m) },
            new[] { new SnapshotRange(1m, 2m) },
            verdicts.ToDictionary(v => v.Mix,
                v => new StepChartVerdict(v.Visibility, 100, 99)),
            1, 1, Meter: 22);
        _stepCharts.Setup(s => s.Get(ChartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankedStepChart("82626", Now, StepChartPayloadCodec.Encode(enriched)));
    }

    [Fact]
    public async Task ProjectsTheAskingMixesView()
    {
        SetupPayload((MixEnum.Phoenix, StepChartVisibility.Full),
            (MixEnum.Phoenix2, StepChartVisibility.StepsOnly));

        var record = await Build().Handle(new GetChartStepChartQuery(ChartId, MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.NotNull(record);
        Assert.Equal("82626", record!.Vintage);
        Assert.Equal(StepChartVisibility.StepsOnly, record.Visibility);
        Assert.Equal(100, record.NoteCount);
        Assert.Equal(99, record.ImpliedTotal);
        var row = Assert.Single(record.Rows);
        Assert.Equal(0.5m, row.Time);
        Assert.Equal(1 | (1 << 4), row.PanelMask);
        Assert.Equal(1, row.LeftFootMask);
        Assert.Equal(1m, row.Beat);
        Assert.True(Assert.Single(record.Holds).IsLeftFoot);
        Assert.Equal(1.5m, Assert.Single(record.TickTimes));
        var segment = Assert.Single(record.Segments);
        Assert.Equal(5.5m, segment.Enps);
        Assert.Equal(new[] { "bracket_run" }, segment.Badges);
        Assert.Equal(21.4m, segment.Level);
        Assert.Equal(22, record.Meter);
    }

    [Fact]
    public async Task AnExcludedMixAnswersNullWhileTheOtherMixStillRenders()
    {
        SetupPayload((MixEnum.Phoenix, StepChartVisibility.Excluded),
            (MixEnum.Phoenix2, StepChartVisibility.Full));

        Assert.Null(await Build().Handle(new GetChartStepChartQuery(ChartId, MixEnum.Phoenix),
            CancellationToken.None));
        Assert.NotNull(await Build().Handle(new GetChartStepChartQuery(ChartId, MixEnum.Phoenix2),
            CancellationToken.None));
    }

    [Fact]
    public async Task AMixWithoutAVerdictAnswersNull()
    {
        SetupPayload((MixEnum.Phoenix, StepChartVisibility.Full));

        Assert.Null(await Build().Handle(new GetChartStepChartQuery(ChartId, MixEnum.XX),
            CancellationToken.None));
    }

    [Fact]
    public async Task NothingBankedAndGarbageBothAnswerNull()
    {
        Assert.Null(await Build().Handle(new GetChartStepChartQuery(ChartId, MixEnum.Phoenix),
            CancellationToken.None));

        _stepCharts.Setup(s => s.Get(ChartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankedStepChart("82626", Now, new byte[] { 9, 9, 9 }));
        Assert.Null(await Build().Handle(new GetChartStepChartQuery(ChartId, MixEnum.Phoenix),
            CancellationToken.None));
    }
}
