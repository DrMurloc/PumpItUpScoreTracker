using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetChartsHandlerTests
{
    [Fact]
    public async Task ReturnsChartsMatchingAllFilters()
    {
        var expected = new[] { new ChartBuilder().Build() };
        var ids = new[] { Guid.NewGuid() };
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(MixEnum.Phoenix, DifficultyLevel.From(20), ChartType.Single,
                It.Is<IEnumerable<Guid>>(g => g != null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetChartsHandler(charts.Object);
        var result = await handler.Handle(
            new GetChartsQuery(MixEnum.Phoenix, DifficultyLevel.From(20), ChartType.Single, ids),
            CancellationToken.None);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task PassesNullFiltersThroughWhenUnspecified()
    {
        var expected = new[] { new ChartBuilder().Build() };
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetChartsHandler(charts.Object);
        var result = await handler.Handle(new GetChartsQuery(MixEnum.Phoenix), CancellationToken.None);

        Assert.Equal(expected, result);
    }
}
