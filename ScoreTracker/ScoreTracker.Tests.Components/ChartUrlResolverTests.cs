using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class ChartUrlResolverTests
{
    private readonly Mock<IMediator> _mediator = new();

    private ChartUrlResolver BuildResolver()
    {
        return new ChartUrlResolver(_mediator.Object, new MemoryCache(new MemoryCacheOptions()));
    }

    private void SetupMix(MixEnum mix, params Chart[] charts)
    {
        _mediator.Setup(m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == mix), It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
    }

    private void SetupEmptyCatalogs()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
    }

    [Fact]
    public async Task HistoricalTripleResolvesAgainstThatMixAndRedirectsToTheCurrentCanonical()
    {
        // The workshop scenario: the chart was D19 in XX, rerated D20 in Phoenix. /xx/…/d19
        // means "the chart that was D19 in XX" — it must land on the Phoenix D20 canonical.
        var chartId = Guid.NewGuid();
        SetupEmptyCatalogs();
        SetupMix(MixEnum.XX, ChartSlugsTests.BuildChart(chartId, mix: MixEnum.XX, level: 19));
        SetupMix(MixEnum.Phoenix, ChartSlugsTests.BuildChart(chartId, level: 20));

        var resolution = await BuildResolver().ResolveHistorical("xx", "baroque-virus-full-song", "d19",
            MixEnum.Phoenix, CancellationToken.None);

        Assert.NotNull(resolution);
        Assert.Equal(chartId, resolution!.ChartId);
        Assert.Equal("/phoenix/baroque-virus-full-song/d20", resolution.CanonicalPath);
    }

    [Fact]
    public async Task UnknownMixSongOrDifficultyResolvesToNothing()
    {
        SetupEmptyCatalogs();
        SetupMix(MixEnum.Phoenix, ChartSlugsTests.BuildChart(level: 20));
        var resolver = BuildResolver();

        Assert.Null(await resolver.ResolveHistorical("not-a-mix", "baroque-virus-full-song", "d20",
            MixEnum.Phoenix, CancellationToken.None));
        Assert.Null(await resolver.ResolveHistorical("phoenix", "no-such-song", "d20",
            MixEnum.Phoenix, CancellationToken.None));
        Assert.Null(await resolver.ResolveHistorical("phoenix", "baroque-virus-full-song", "d15",
            MixEnum.Phoenix, CancellationToken.None));
    }

    [Fact]
    public async Task ChartsAbsentFromTheDefaultMixFallBackToTheNewestMixCarryingThem()
    {
        // Removed content keeps a canonical: the newest catalog that still carries it.
        var chartId = Guid.NewGuid();
        SetupEmptyCatalogs();
        SetupMix(MixEnum.XX, ChartSlugsTests.BuildChart(chartId, mix: MixEnum.XX, level: 19));

        var canonical = await BuildResolver()
            .CanonicalPathFor(chartId, MixEnum.Phoenix, CancellationToken.None);

        Assert.Equal("/xx/baroque-virus-full-song/d19", canonical);
    }

    [Fact]
    public async Task CatalogReadsAreCachedPerMix()
    {
        var chartId = Guid.NewGuid();
        SetupEmptyCatalogs();
        SetupMix(MixEnum.Phoenix, ChartSlugsTests.BuildChart(chartId, level: 20));
        var resolver = BuildResolver();

        await resolver.CanonicalPathFor(chartId, MixEnum.Phoenix, CancellationToken.None);
        await resolver.CanonicalPathFor(chartId, MixEnum.Phoenix, CancellationToken.None);

        _mediator.Verify(m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == MixEnum.Phoenix),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
