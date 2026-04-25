using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetCoOpRatingHandlerTests
{
    [Fact]
    public async Task DelegatesToRepository()
    {
        var chartId = Guid.NewGuid();
        var rating = new CoOpRating(chartId, 1, new Dictionary<int, DifficultyLevel> { { 1, DifficultyLevel.From(20) } });
        var ratings = new Mock<IChartDifficultyRatingRepository>();
        ratings.Setup(r => r.GetCoOpRating(chartId, It.IsAny<CancellationToken>())).ReturnsAsync(rating);

        var handler = new GetCoOpRatingHandler(ratings.Object);
        var result = await handler.Handle(new GetCoOpRatingQuery(chartId), CancellationToken.None);

        Assert.Equal(rating, result);
    }

    [Fact]
    public async Task ReturnsNullWhenRepositoryReturnsNull()
    {
        var ratings = new Mock<IChartDifficultyRatingRepository>();
        ratings.Setup(r => r.GetCoOpRating(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CoOpRating?)null);

        var handler = new GetCoOpRatingHandler(ratings.Object);
        var result = await handler.Handle(new GetCoOpRatingQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }
}
