using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Catalog.Application;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetHoldTickProfileHandlerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IChartSkillMetricRepository> _metrics = new();

    private GetHoldTickProfileHandler BuildHandler()
    {
        return new GetHoldTickProfileHandler(_mediator.Object, _metrics.Object,
            new MemoryCache(new MemoryCacheOptions()));
    }

    private void SetupCharts(MixEnum mix, params Chart[] charts)
    {
        _mediator.Setup(m => m.Send(new GetChartsQuery(mix), It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
    }

    /// <summary>
    ///     The crawl banks the note-shape trio together, so the fixture speaks it too — the
    ///     trust check reads the file's tick total, and a fixture without one would test the
    ///     benefit-of-the-doubt path that no measured chart can reach.
    /// </summary>
    private void SetupMetrics(params (Guid ChartId, int TapRows, int HoldRows, int HoldTicks)[] rows)
    {
        _metrics.Setup(m => m.GetMetricsByChart(PiuCenterMetrics.Source, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows.ToDictionary(
                r => r.ChartId,
                r => (IReadOnlyList<ChartSkillMetric>)new[]
                {
                    new ChartSkillMetric(r.ChartId, PiuCenterMetrics.TapRows, r.TapRows, null),
                    new ChartSkillMetric(r.ChartId, PiuCenterMetrics.HoldRows, r.HoldRows, null),
                    new ChartSkillMetric(r.ChartId, PiuCenterMetrics.HoldTicks, r.HoldTicks, null)
                }));
    }

    [Fact]
    public async Task SharesDeriveFromNoteCountMinusTapRowsPerLevel()
    {
        // Six S18 charts at an even spread of tick shares — enough to clear the per-level
        // floor — and the profile reads their median and the extremes lists.
        var charts = Enumerable.Range(0, 6)
            .Select(i => new ChartBuilder().WithLevel(18).WithNoteCount(1000).Build())
            .ToArray();
        SetupCharts(MixEnum.Phoenix, charts);
        SetupMetrics(charts.Select((c, i) => (c.Id, 1000 - i * 100, 10, i * 100)).ToArray());

        var profile = await BuildHandler().Handle(new GetHoldTickProfileQuery(MixEnum.Phoenix),
            CancellationToken.None);

        Assert.Equal(6, profile.ChartsMeasured);
        var level = Assert.Single(profile.Levels);
        Assert.Equal(18, level.Level);
        Assert.Equal(6, level.Charts);
        // Shares run 0, .1, .2, .3, .4, .5 — the rounded median index lands on .3.
        Assert.Equal(.3, level.MedianShare, 3);
        Assert.Equal(.5, profile.MostTicks.First().Share, 3);
        Assert.Equal(0, profile.FewestTicksFifteenPlus.First().Share, 3);
    }

    [Fact]
    public async Task ReSteppedChartsAreGatedOut()
    {
        // One chart with more taps than judgements (negative ticks) and one whose simfile has
        // no holds yet a total its taps cannot explain — both re-steps, neither measurable.
        // The second now falls to the trust check: zero file ticks cannot account for any
        // inferred hold, the ratio-infinite end of the same veto the identity chips run.
        var negative = new ChartBuilder().WithLevel(16).WithNoteCount(500).Build();
        var noHoldOverrun = new ChartBuilder().WithLevel(16).WithNoteCount(830).Build();
        var clean = new ChartBuilder().WithLevel(16).WithNoteCount(1000).Build();
        SetupCharts(MixEnum.Phoenix, negative, noHoldOverrun, clean);
        SetupMetrics((negative.Id, 600, 40, 200), (noHoldOverrun.Id, 442, 0, 0), (clean.Id, 600, 50, 400));

        var profile = await BuildHandler().Handle(new GetHoldTickProfileQuery(MixEnum.Phoenix),
            CancellationToken.None);

        var measured = Assert.Single(profile.MostTicks);
        Assert.Equal(clean.Id, measured.ChartId);
        Assert.Equal(400, measured.HoldTicks);
        Assert.Equal(1, profile.ChartsMeasured);
    }

    [Fact]
    public async Task PhoenixTwoFallsBackToThePhoenixNoteCountWhereItsOwnIsNull()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var phoenixTwin = chart with { NoteCount = 900 };
        SetupCharts(MixEnum.Phoenix2, chart);
        SetupCharts(MixEnum.Phoenix, phoenixTwin);
        SetupMetrics((chart.Id, 450, 30, 450));

        var profile = await BuildHandler().Handle(new GetHoldTickProfileQuery(MixEnum.Phoenix2),
            CancellationToken.None);

        var measured = Assert.Single(profile.MostTicks);
        Assert.Equal(900, measured.NoteCount);
        Assert.Equal(450, measured.HoldTicks);
        Assert.Equal(.5, measured.Share, 3);
    }

    [Fact]
    public async Task AnEmptyProfileIsNeverCachedSoTheSnapshotUploadShowsUpImmediately()
    {
        // The admin upload is what fills the metrics. A cached "nothing there" would swallow
        // it for a day — the exact "I reran the import and nothing happened" report.
        var chart = new ChartBuilder().WithLevel(18).WithNoteCount(1000).Build();
        SetupCharts(MixEnum.Phoenix, chart);
        SetupMetrics();
        var handler = BuildHandler();

        Assert.Equal(0, (await handler.Handle(new GetHoldTickProfileQuery(MixEnum.Phoenix),
            CancellationToken.None)).ChartsMeasured);

        SetupMetrics((chart.Id, 600, 50, 400));
        var afterUpload = await handler.Handle(new GetHoldTickProfileQuery(MixEnum.Phoenix),
            CancellationToken.None);
        Assert.Equal(1, afterUpload.ChartsMeasured);

        // A measured profile caches: the third call never reaches the repository again.
        await handler.Handle(new GetHoldTickProfileQuery(MixEnum.Phoenix), CancellationToken.None);
        _metrics.Verify(m => m.GetMetricsByChart(PiuCenterMetrics.Source, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    /// <summary>
    ///     Destination SHORT CUT D20, the identity veto's own calibration case: a holdy file
    ///     that still cannot account for the ticks the note count implies (525 inferred, 123 in
    ///     the file). Before the calculator shared the veto it could headline the drenched list
    ///     while its chart page refused it the chip (owner, 2026-08-30).
    /// </summary>
    [Fact]
    public async Task AFileThatCannotAccountForItsInferredTicksIsGatedOut()
    {
        var destination = new ChartBuilder().WithLevel(20).WithNoteCount(803).Build();
        var iolite = new ChartBuilder().WithLevel(20).WithNoteCount(1000).Build();
        SetupCharts(MixEnum.Phoenix, destination, iolite);
        SetupMetrics((destination.Id, 278, 30, 123), (iolite.Id, 156, 290, 848));

        var profile = await BuildHandler().Handle(new GetHoldTickProfileQuery(MixEnum.Phoenix),
            CancellationToken.None);

        var measured = Assert.Single(profile.MostTicks);
        Assert.Equal(iolite.Id, measured.ChartId);
        Assert.Equal(1, profile.ChartsMeasured);
    }

    [Fact]
    public async Task ChartsWithoutBankedTapRowsAreAbsentNotZero()
    {
        var unbanked = new ChartBuilder().WithLevel(18).WithNoteCount(1000).Build();
        SetupCharts(MixEnum.Phoenix, unbanked);
        SetupMetrics();

        var profile = await BuildHandler().Handle(new GetHoldTickProfileQuery(MixEnum.Phoenix),
            CancellationToken.None);

        Assert.Equal(0, profile.ChartsMeasured);
        Assert.Empty(profile.Levels);
    }
}
