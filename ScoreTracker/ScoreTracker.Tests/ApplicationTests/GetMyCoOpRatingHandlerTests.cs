using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetMyCoOpRatingHandlerTests
{
    [Fact]
    public async Task DelegatesToRepositoryWithCurrentUserId()
    {
        var user = new UserBuilder().Build();
        var chartId = Guid.NewGuid();
        var rating = new Dictionary<int, DifficultyLevel>
        {
            { 1, DifficultyLevel.From(20) }, { 2, DifficultyLevel.From(22) }
        };
        var ratings = new Mock<IChartDifficultyRatingRepository>();
        ratings.Setup(r => r.GetMyCoOpRating(user.Id, chartId, It.IsAny<CancellationToken>())).ReturnsAsync(rating);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new GetMyCoOpRatingHandler(ratings.Object, currentUser.Object);
        var result = await handler.Handle(new GetMyCoOpRatingQuery(chartId), CancellationToken.None);

        Assert.Same(rating, result);
    }
}
