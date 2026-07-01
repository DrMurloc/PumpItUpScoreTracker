using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetAllChartScoreAggregatesHandlerTests
{
    [Fact]
    public async Task ReturnsAggregatesFromRepository()
    {
        var aggregates = new List<ChartScoreAggregate>();
        var records = new Mock<IPhoenixRecordRepository>();
        records.Setup(r => r.GetAllChartScoreAggregates(It.IsAny<CancellationToken>())).ReturnsAsync(aggregates);

        var handler = new GetAllChartScoreAggregatesHandler(records.Object);
        var result = await handler.Handle(new GetAllChartScoreAggregatesQuery(), CancellationToken.None);

        Assert.Same(aggregates, result);
    }
}
