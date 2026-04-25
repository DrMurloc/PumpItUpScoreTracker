using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetPhoenixScoresForChartHandlerTests
{
    [Fact]
    public async Task DelegatesToRepository()
    {
        var chartId = Guid.NewGuid();
        var scores = new List<UserPhoenixScore>();
        var records = new Mock<IPhoenixRecordRepository>();
        records.Setup(r => r.GetRecordedUserScores(chartId, It.IsAny<CancellationToken>())).ReturnsAsync(scores);

        var handler = new GetPhoenixScoresForChartHandler(records.Object);
        var result = await handler.Handle(new GetPhoenixScoresForChartQuery(chartId), CancellationToken.None);

        Assert.Same(scores, result);
    }
}
