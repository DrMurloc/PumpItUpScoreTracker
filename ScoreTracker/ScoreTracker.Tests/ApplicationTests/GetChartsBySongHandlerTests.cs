using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Commands;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Catalog.Application;
using ScoreTracker.Application.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetChartsBySongHandlerTests
{
    [Fact]
    public async Task ReturnsChartsForSong()
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
