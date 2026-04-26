using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetPhoenixRecordHandlerTests
{
    [Fact]
    public async Task ReturnsRecordedScoreForCurrentUser()
    {
        var user = new UserBuilder().Build();
        var chartId = Guid.NewGuid();
        var score = new RecordedPhoenixScore(chartId, 995010, PhoenixPlate.MarvelousGame, false,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var records = new Mock<IPhoenixRecordRepository>();
        records.Setup(r => r.GetRecordedScore(user.Id, chartId, It.IsAny<CancellationToken>())).ReturnsAsync(score);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new GetPhoenixRecordHandler(currentUser.Object, records.Object);
        var result = await handler.Handle(new GetPhoenixRecordQuery(chartId), CancellationToken.None);

        Assert.Equal(score, result);
    }

    [Fact]
    public async Task ReturnsNullWhenNoRecordExists()
    {
        var user = new UserBuilder().Build();
        var records = new Mock<IPhoenixRecordRepository>();
        records.Setup(r => r.GetRecordedScore(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecordedPhoenixScore?)null);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new GetPhoenixRecordHandler(currentUser.Object, records.Object);
        var result = await handler.Handle(new GetPhoenixRecordQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }
}
