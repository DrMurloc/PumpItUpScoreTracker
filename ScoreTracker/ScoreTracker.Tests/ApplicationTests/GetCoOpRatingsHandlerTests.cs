using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Commands;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Application;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetCoOpRatingsHandlerTests
{
    [Fact]
    public async Task ReturnsAllCoOpRatings()
    {
        var ratingsList = new List<CoOpRating>();
        var ratings = new Mock<IChartDifficultyRatingRepository>();
        ratings.Setup(r => r.GetAllCoOpRatings(It.IsAny<CancellationToken>())).ReturnsAsync(ratingsList);

        var handler = new GetCoOpRatingsHandler(ratings.Object);
        var result = await handler.Handle(new GetCoOpRatingsQuery(), CancellationToken.None);

        Assert.Same(ratingsList, result);
    }
}
