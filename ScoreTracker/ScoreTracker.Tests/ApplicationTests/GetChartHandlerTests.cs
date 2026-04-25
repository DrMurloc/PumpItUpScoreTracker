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

public sealed class GetChartHandlerTests
{
    [Fact]
    public async Task ReturnsChartMatchingTypeAndLevel()
    {
        var matching = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var wrongLevel = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var wrongType = new ChartBuilder().WithType(ChartType.Double).WithLevel(15).Build();

        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetChartsForSong(MixEnum.Phoenix, It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { wrongLevel, matching, wrongType });

        var handler = new GetChartHandler(charts.Object);
        var result = await handler.Handle(
            new GetChartQuery(MixEnum.Phoenix, Name.From("song"), DifficultyLevel.From(15), ChartType.Single),
            CancellationToken.None);

        Assert.Equal(matching, result);
    }

    [Fact]
    public async Task ReturnsNullWhenNoChartMatches()
    {
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetChartsForSong(It.IsAny<MixEnum>(), It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ChartBuilder().WithType(ChartType.Double).WithLevel(20).Build() });

        var handler = new GetChartHandler(charts.Object);
        var result = await handler.Handle(
            new GetChartQuery(MixEnum.Phoenix, Name.From("song"), DifficultyLevel.From(15), ChartType.Single),
            CancellationToken.None);

        Assert.Null(result);
    }
}
