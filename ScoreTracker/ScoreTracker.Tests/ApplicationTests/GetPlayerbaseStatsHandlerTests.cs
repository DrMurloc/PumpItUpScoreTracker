using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Application;
using ScoreTracker.Identity.Contracts.Queries;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetPlayerbaseStatsHandlerTests
{
    [Fact]
    public async Task ReturnsTheRepositoryCounts()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetPlayerbaseCounts(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerbaseCounts(2359, 39));
        var handler = new GetPlayerbaseStatsHandler(users.Object);

        var result = await handler.Handle(new GetPlayerbaseStatsQuery(), CancellationToken.None);

        Assert.Equal(2359, result.Players);
        Assert.Equal(39, result.Countries);
    }
}
