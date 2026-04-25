using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetChartRatingsHandlerTests
{
    [Fact]
    public async Task ReturnsAllRatingsForAnonymousUser()
    {
        var record = new ChartDifficultyRatingRecord(Guid.NewGuid(), 20.0, 5, 0.3);
        var ratings = new Mock<IChartDifficultyRatingRepository>();
        ratings.Setup(r => r.GetAllChartRatedDifficulties(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { record });
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.IsLoggedIn).Returns(false);

        var handler = new GetChartRatingsHandler(ratings.Object, currentUser.Object,
            new Mock<IChartRepository>().Object);
        var result = (await handler.Handle(new GetChartRatingsQuery(MixEnum.Phoenix), CancellationToken.None))
            .ToArray();

        Assert.Single(result);
        Assert.Null(result[0].MyRating);
    }

    [Fact]
    public async Task FiltersByLevelAndType()
    {
        var matchingId = Guid.NewGuid();
        var nonMatchingId = Guid.NewGuid();
        var matching = new ChartDifficultyRatingRecord(matchingId, 20.0, 5, 0.3);
        var nonMatching = new ChartDifficultyRatingRecord(nonMatchingId, 10.0, 1, 0.0);
        var ratings = new Mock<IChartDifficultyRatingRepository>();
        ratings.Setup(r => r.GetAllChartRatedDifficulties(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { matching, nonMatching });
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(MixEnum.Phoenix, It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<System.Collections.Generic.IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ChartBuilder().WithId(matchingId).Build() });
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.IsLoggedIn).Returns(false);

        var handler = new GetChartRatingsHandler(ratings.Object, currentUser.Object, charts.Object);
        var result = (await handler.Handle(
                new GetChartRatingsQuery(MixEnum.Phoenix, DifficultyLevel.From(20), ChartType.Single),
                CancellationToken.None))
            .ToArray();

        Assert.Single(result);
        Assert.Equal(matchingId, result[0].ChartId);
    }

    [Fact]
    public async Task PopulatesMyRatingForLoggedInUser()
    {
        var chartId = Guid.NewGuid();
        var record = new ChartDifficultyRatingRecord(chartId, 20.0, 5, 0.3);
        var user = new UserBuilder().Build();
        var ratings = new Mock<IChartDifficultyRatingRepository>();
        ratings.Setup(r => r.GetAllChartRatedDifficulties(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { record });
        ratings.Setup(r => r.GetRatingsByUser(MixEnum.Phoenix, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { (chartId, DifficultyAdjustment.Hard) });
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new GetChartRatingsHandler(ratings.Object, currentUser.Object,
            new Mock<IChartRepository>().Object);
        var result = (await handler.Handle(new GetChartRatingsQuery(MixEnum.Phoenix), CancellationToken.None))
            .ToArray();

        Assert.Single(result);
        Assert.Equal(DifficultyAdjustment.Hard, result[0].MyRating);
    }
}
