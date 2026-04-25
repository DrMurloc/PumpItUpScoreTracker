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

public sealed class GetChartsBySongHandlerTests
{
    [Fact]
    public async Task DelegatesToRepository()
    {
        var songName = Name.From("Test Song");
        var expected = new[] { new ChartBuilder().Build() };
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetChartsForSong(MixEnum.Phoenix, songName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetChartsBySongHandler(charts.Object);
        var result = await handler.Handle(new GetChartsBySongQuery(MixEnum.Phoenix, songName), CancellationToken.None);

        Assert.Equal(expected, result);
    }
}
