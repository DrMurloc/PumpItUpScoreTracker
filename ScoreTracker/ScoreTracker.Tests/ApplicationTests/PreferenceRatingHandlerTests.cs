using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PreferenceRatingHandlerTests
{
    [Fact]
    public async Task UpdateSavesRatingThenWritesAverage()
    {
        var user = new UserBuilder().Build();
        var chartId = Guid.NewGuid();
        var preferences = new Mock<IChartPreferenceRepository>();
        preferences.Setup(p => p.GetRatingsForChart(MixEnum.Phoenix, chartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { PreferenceRating.From(4m), PreferenceRating.From(2m) });
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new PreferenceRatingHandler(preferences.Object, currentUser.Object);
        var result = await handler.Handle(
            new UpdatePreferenceRatingCommand(MixEnum.Phoenix, chartId, PreferenceRating.From(4m)),
            CancellationToken.None);

        preferences.Verify(p => p.SaveRating(MixEnum.Phoenix, user.Id, chartId, PreferenceRating.From(4m),
            It.IsAny<CancellationToken>()), Times.Once);
        preferences.Verify(p => p.SetAverageRating(MixEnum.Phoenix, chartId, PreferenceRating.From(3m), 2,
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, result.Count);
        Assert.Equal(PreferenceRating.From(3m), result.Rating);
    }

    [Fact]
    public async Task GetAllDelegatesToRepository()
    {
        var ratings = new List<ChartPreferenceRatingRecord>();
        var preferences = new Mock<IChartPreferenceRepository>();
        preferences.Setup(p => p.GetPreferenceRatings(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ratings);

        var handler = new PreferenceRatingHandler(preferences.Object, new Mock<ICurrentUserAccessor>().Object);
        var result = await handler.Handle(new GetAllPreferenceRatingsQuery(MixEnum.Phoenix), CancellationToken.None);

        Assert.Same(ratings, result);
    }

    [Fact]
    public async Task GetUserUsesCurrentUserId()
    {
        var user = new UserBuilder().Build();
        var ratings = new List<UserRatingsRecord>();
        var preferences = new Mock<IChartPreferenceRepository>();
        preferences.Setup(p => p.GetUserRatings(MixEnum.Phoenix, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ratings);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new PreferenceRatingHandler(preferences.Object, currentUser.Object);
        var result = await handler.Handle(new GetUserPreferenceRatingsQuery(MixEnum.Phoenix), CancellationToken.None);

        Assert.Same(ratings, result);
    }
}
