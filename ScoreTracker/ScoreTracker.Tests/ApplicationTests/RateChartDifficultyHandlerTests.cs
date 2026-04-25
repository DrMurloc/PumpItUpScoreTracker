using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class RateChartDifficultyHandlerTests
{
    [Fact]
    public async Task PersistsRatingAndChainsRecalculate()
    {
        var user = new UserBuilder().Build();
        var chartId = Guid.NewGuid();
        var resultRecord = new ChartDifficultyRatingRecord(chartId, 16.0, 3, 0.2);
        var ratings = new Mock<IChartDifficultyRatingRepository>();
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.Is<ReCalculateChartRatingCommand>(c => c.ChartId == chartId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultRecord);

        var handler = new RateChartDifficultyHandler(ratings.Object, currentUser.Object, mediator.Object);
        var result = await handler.Handle(
            new RateChartDifficultyCommand(MixEnum.Phoenix, chartId, DifficultyAdjustment.Hard),
            CancellationToken.None);

        ratings.Verify(r => r.RateChart(MixEnum.Phoenix, chartId, user.Id, DifficultyAdjustment.Hard,
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(resultRecord, result);
    }
}
