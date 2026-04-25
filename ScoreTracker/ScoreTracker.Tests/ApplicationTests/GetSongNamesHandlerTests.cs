using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetSongNamesHandlerTests
{
    [Fact]
    public async Task DelegatesToRepository()
    {
        var names = new[] { Name.From("song-a"), Name.From("song-b") };
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetSongNames(MixEnum.Phoenix, It.IsAny<CancellationToken>())).ReturnsAsync(names);

        var handler = new GetSongNamesHandler(charts.Object);
        var result = await handler.Handle(new GetSongNamesQuery(MixEnum.Phoenix), CancellationToken.None);

        Assert.Equal(names, result);
    }
}
