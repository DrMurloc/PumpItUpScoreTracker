using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetPhoenixScoresForChartHandlerTests
{
    [Fact]
    public async Task ReturnsRecordedUserScoresForChart()
    {
        var chartId = Guid.NewGuid();
        var scores = new List<UserPhoenixScore>();
        var records = new Mock<IPhoenixRecordRepository>();
        records.Setup(r => r.GetRecordedUserScores(MixEnum.Phoenix, chartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scores);

        var handler = new GetPhoenixScoresForChartHandler(records.Object);
        var result = await handler.Handle(new GetPhoenixScoresForChartQuery(chartId), CancellationToken.None);

        Assert.Same(scores, result);
    }
}
