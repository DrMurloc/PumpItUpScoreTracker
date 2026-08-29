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
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The seam where a chart's profile meets a mix's judged note count
///     (docs/design/chart-identity.md §3.9) — the one per-mix input the identity engine has,
///     and the one read the handler adds beyond what the metrics already carry.
/// </summary>
public sealed class ChartIdentityHandlerTests
{
    private readonly Mock<IArchivedSkillTagRepository> _archive = new();
    private readonly Mock<IChartFolderBaselineRepository> _baselines = new();
    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<IChartSkillMetricRepository> _metrics = new();

    private ChartIdentityHandler BuildHandler()
    {
        return new ChartIdentityHandler(_metrics.Object, _charts.Object, _baselines.Object, _archive.Object);
    }

    private void SetupChart(Guid chartId, MixEnum mix, int? noteCount)
    {
        _charts.Setup(c => c.GetCharts(mix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ChartBuilder().WithId(chartId).WithType(ChartType.Double).WithLevel(22)
                    .WithNoteCount(noteCount).Build()
            });
    }

    private void SetupHoldWorld(Guid chartId)
    {
        _metrics.Setup(m => m.GetMetrics(It.IsAny<IEnumerable<Guid>>(), PiuCenterMetrics.Source,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ChartSkillMetric(chartId, PiuCenterMetrics.TapRows, 354m, null),
                new ChartSkillMetric(chartId, PiuCenterMetrics.HoldTicks, 740m, null)
            });
        // A folder whose p90 sits at 0.610 — the real D22 number That Kitty clears at 0.674.
        _baselines.Setup(b => b.GetFolderBaselines(It.IsAny<MixEnum>(), ChartType.Double, 22,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ChartFolderBaseline>(StringComparer.OrdinalIgnoreCase)
            {
                [PiuCenterMetrics.HoldShare] = new(MixEnum.Phoenix2, ChartType.Double, 22,
                    PiuCenterMetrics.HoldShare, 0.35m, 0.61m, 0m, 20, 20)
            });
    }

    /// <summary>
    ///     §3.9. Phoenix 2's judged counts are still refilling from play, so a null borrows
    ///     Phoenix 1's — the same fallback the baseline sweep uses. 354 banked steps against a
    ///     judged 1,087 is a 0.674 hold share, over the folder's 0.61 bar.
    /// </summary>
    [Fact]
    public async Task APhoenix2ChartWithoutItsOwnCountBorrowsPhoenix1sForTheHoldShare()
    {
        var chartId = Guid.NewGuid();
        SetupChart(chartId, MixEnum.Phoenix2, null);
        SetupChart(chartId, MixEnum.Phoenix, 1087);
        SetupHoldWorld(chartId);

        var result = await BuildHandler().Handle(
            new GetChartIdentityQuery(new[] { chartId }, MixEnum.Phoenix2), CancellationToken.None);

        var chip = Assert.Single(result[chartId].Chips, c => c.Kind == IdentityChipKind.Holds);
        Assert.Equal(IdentityClaimKeys.HoldHeavy, chip.Badge);
        Assert.Equal(IdentityTier.Identity, chip.Tier);
    }

    /// <summary>
    ///     A mix that carries its own count never pays for the fallback read — the Phoenix
    ///     catalog is only fetched when a Phoenix 2 chart actually needs it.
    /// </summary>
    [Fact]
    public async Task AChartCarryingItsOwnCountNeverTriggersTheFallbackRead()
    {
        var chartId = Guid.NewGuid();
        SetupChart(chartId, MixEnum.Phoenix2, 1087);
        SetupHoldWorld(chartId);

        var result = await BuildHandler().Handle(
            new GetChartIdentityQuery(new[] { chartId }, MixEnum.Phoenix2), CancellationToken.None);

        Assert.Contains(result[chartId].Chips, c => c.Badge == IdentityClaimKeys.HoldHeavy);
        _charts.Verify(c => c.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
