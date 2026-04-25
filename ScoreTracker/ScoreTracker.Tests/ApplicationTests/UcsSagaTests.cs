using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class UcsSagaTests
{
    [Fact]
    public async Task RegisterUpdatesScoreThenPublishesPlacedEvent()
    {
        var user = new UserBuilder().Build();
        var chartId = Guid.NewGuid();
        var bus = new Mock<IBus>();
        var ucs = new Mock<IUcsRepository>();
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var saga = new UcsSaga(bus.Object, ucs.Object, currentUser.Object);
        await saga.Handle(
            new RegisterUcsEntryCommand(chartId, PhoenixScore.From(950000), PhoenixPlate.SuperbGame, IsBroken: false,
                VideoPath: null, ImagePath: null),
            CancellationToken.None);

        ucs.Verify(u => u.UpdateScore(chartId, user.Id, PhoenixScore.From(950000), PhoenixPlate.SuperbGame,
            false, null, null, It.IsAny<CancellationToken>()), Times.Once);
        bus.Verify(b => b.Publish(
            It.Is<UcsLeaderboardPlacedEvent>(e => e.UserId == user.Id && e.ChartId == chartId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PassesVideoAndImagePathsThrough()
    {
        var user = new UserBuilder().Build();
        var chartId = Guid.NewGuid();
        var video = new Uri("https://example.invalid/v.mp4");
        var image = new Uri("https://example.invalid/i.png");
        var bus = new Mock<IBus>();
        var ucs = new Mock<IUcsRepository>();
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var saga = new UcsSaga(bus.Object, ucs.Object, currentUser.Object);
        await saga.Handle(
            new RegisterUcsEntryCommand(chartId, PhoenixScore.From(800000), PhoenixPlate.MarvelousGame, IsBroken: true,
                VideoPath: video, ImagePath: image),
            CancellationToken.None);

        ucs.Verify(u => u.UpdateScore(chartId, user.Id, PhoenixScore.From(800000), PhoenixPlate.MarvelousGame,
            true, video, image, It.IsAny<CancellationToken>()), Times.Once);
    }
}
