using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetChartRatingHandlerTests
{
    [Fact]
    public async Task ReturnsBaseRatingWhenAnonymous()
    {
        var chartId = Guid.NewGuid();
        var record = new ChartDifficultyRatingRecord(chartId, 15.5, 10, 0.5);
        var ratings = new Mock<IChartDifficultyRatingRepository>();
        ratings.Setup(r => r.GetChartRatedDifficulty(MixEnum.Phoenix, chartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.IsLoggedIn).Returns(false);

        var handler = new GetChartRatingHandler(ratings.Object, currentUser.Object);
        var result = await handler.Handle(new GetChartRatingQuery(MixEnum.Phoenix, chartId), CancellationToken.None);

        Assert.Equal(record, result);
        Assert.Null(result!.MyRating);
        ratings.Verify(r => r.GetRating(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReturnsNullWhenChartNotRated()
    {
        var ratings = new Mock<IChartDifficultyRatingRepository>();
        ratings.Setup(r =>
                r.GetChartRatedDifficulty(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChartDifficultyRatingRecord?)null);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.IsLoggedIn).Returns(true);

        var handler = new GetChartRatingHandler(ratings.Object, currentUser.Object);
        var result = await handler.Handle(new GetChartRatingQuery(MixEnum.Phoenix, Guid.NewGuid()),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task PopulatesMyRatingForLoggedInUser()
    {
        var chartId = Guid.NewGuid();
        var record = new ChartDifficultyRatingRecord(chartId, 15.5, 10, 0.5);
        var user = new UserBuilder().Build();
        var ratings = new Mock<IChartDifficultyRatingRepository>();
        ratings.Setup(r => r.GetChartRatedDifficulty(MixEnum.Phoenix, chartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        ratings.Setup(r => r.GetRating(MixEnum.Phoenix, chartId, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DifficultyAdjustment.Easy);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new GetChartRatingHandler(ratings.Object, currentUser.Object);
        var result = await handler.Handle(new GetChartRatingQuery(MixEnum.Phoenix, chartId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DifficultyAdjustment.Easy, result!.MyRating);
    }
}
