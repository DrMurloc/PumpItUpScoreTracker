using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Catalog.Application;
using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class MixVersionHandlersTests
{
    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<IMixVersionRepository> _versions = new();

    private static readonly Guid Launch = Guid.NewGuid();
    private static readonly Guid Patch = Guid.NewGuid();

    [Fact]
    public async Task VersionsComeBackInReleaseOrderWithTheChartsEachOneAdded()
    {
        _versions.Setup(v => v.GetVersions(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MixVersion(Patch, MixEnum.Phoenix2, "1.01.0", new DateOnly(2026, 9, 3), 20),
                new MixVersion(Launch, MixEnum.Phoenix2, "1.00.0", new DateOnly(2026, 7, 9), 10)
            });
        _charts.Setup(c => c.GetCharts(MixEnum.Phoenix2, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Chart[]
            {
                new ChartBuilder().WithMix(MixEnum.Phoenix2).WithRelease("1.00.0", new DateOnly(2026, 7, 9), 10),
                new ChartBuilder().WithMix(MixEnum.Phoenix2).WithRelease("1.00.0", new DateOnly(2026, 7, 9), 10),
                new ChartBuilder().WithMix(MixEnum.Phoenix2).WithRelease("1.01.0", new DateOnly(2026, 9, 3), 20),
                new ChartBuilder().WithMix(MixEnum.Phoenix2)
            });

        var result = await new GetMixVersionsHandler(_versions.Object, _charts.Object)
            .Handle(new GetMixVersionsQuery(MixEnum.Phoenix2), CancellationToken.None);

        Assert.Equal(new[] { "1.00.0", "1.01.0" }, result.Select(r => r.Name));
        Assert.Equal(new[] { 2, 1 }, result.Select(r => r.ChartCount));
        Assert.Equal(new DateOnly(2026, 9, 3), result[1].ReleaseDate);
        Assert.Equal(20, result[1].SortOrder);
        Assert.All(result, r => Assert.Equal(MixEnum.Phoenix2, r.Mix));
    }

    [Fact]
    public async Task AMixWithNoPatchesAnswersEmptyWithoutReadingTheCatalog()
    {
        _versions.Setup(v => v.GetVersions(MixEnum.Pro, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MixVersion>());

        var result = await new GetMixVersionsHandler(_versions.Object, _charts.Object)
            .Handle(new GetMixVersionsQuery(MixEnum.Pro), CancellationToken.None);

        Assert.Empty(result);
        _charts.Verify(c => c.GetCharts(It.IsAny<MixEnum>(), null, null, null, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatingAVersionTrimsTheNameAndReturnsTheRepositoryId()
    {
        var id = Guid.NewGuid();
        _versions.Setup(v => v.Create(MixEnum.Phoenix2, "1.02.0", new DateOnly(2026, 11, 5), It.IsAny<CancellationToken>()))
            .ReturnsAsync(id);

        var result = await new CreateMixVersionHandler(_versions.Object)
            .Handle(new CreateMixVersionCommand(MixEnum.Phoenix2, " 1.02.0 ", new DateOnly(2026, 11, 5)), CancellationToken.None);

        Assert.Equal(id, result);
    }

    [Fact]
    public async Task ABlankNameIsRefusedBeforeItReachesTheRepository()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => new CreateMixVersionHandler(_versions.Object)
            .Handle(new CreateMixVersionCommand(MixEnum.Phoenix2, "   ", null), CancellationToken.None));

        _versions.Verify(v => v.Create(It.IsAny<MixEnum>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
