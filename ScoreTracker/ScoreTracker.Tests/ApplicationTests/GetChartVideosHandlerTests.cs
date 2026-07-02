using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Commands;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Catalog.Application;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetChartVideosHandlerTests
{
    [Fact]
    public async Task ReturnsVideoInformationForRequestedCharts()
    {
        var ids = new[] { Guid.NewGuid() };
        var expected = new List<ChartVideoInformation>();
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetChartVideoInformation(ids, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var handler = new GetChartVideosHandler(charts.Object);
        var result = await handler.Handle(new GetChartVideosQuery(ids), CancellationToken.None);

        Assert.Same(expected, result);
    }
}
