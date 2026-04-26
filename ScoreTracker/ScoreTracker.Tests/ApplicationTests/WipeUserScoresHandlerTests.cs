using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.SecondaryPorts;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class WipeUserScoresHandlerTests
{
    private static (WipeUserScoresHandler handler,
        Mock<IPhoenixRecordRepository> phoenix,
        Mock<IXXChartAttemptRepository> xx,
        Mock<IPlayerStatsRepository> stats,
        Mock<ITitleRepository> titles,
        Mock<IPlayerHistoryRepository> history,
        Mock<IBus> bus) BuildHandler()
    {
        var phoenix = new Mock<IPhoenixRecordRepository>();
        var xx = new Mock<IXXChartAttemptRepository>();
        var stats = new Mock<IPlayerStatsRepository>();
        var titles = new Mock<ITitleRepository>();
        var history = new Mock<IPlayerHistoryRepository>();
        var bus = new Mock<IBus>();
        var handler = new WipeUserScoresHandler(phoenix.Object, xx.Object, stats.Object, titles.Object, history.Object,
            bus.Object);
        return (handler, phoenix, xx, stats, titles, history, bus);
    }

    [Fact]
    public async Task DeletesScoresStatsTitlesAndPublishesRecomputeEventWhenHistoryNotIncluded()
    {
        var (handler, phoenix, xx, stats, titles, history, bus) = BuildHandler();
        var userId = Guid.NewGuid();

        await handler.Handle(new WipeUserScoresCommand(userId, false), CancellationToken.None);

        phoenix.Verify(p => p.DeleteAllForUser(userId, It.IsAny<CancellationToken>()), Times.Once);
        xx.Verify(p => p.DeleteAllForUser(userId, It.IsAny<CancellationToken>()), Times.Once);
        stats.Verify(p => p.DeleteStats(userId, It.IsAny<CancellationToken>()), Times.Once);
        titles.Verify(p => p.DeleteHighestTitle(userId, It.IsAny<CancellationToken>()), Times.Once);
        history.Verify(p => p.DeleteHistoryForUser(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        bus.Verify(
            b => b.Publish(
                It.Is<PlayerScoreUpdatedEvent>(e =>
                    e.UserId == userId && e.NewChartIds.Length == 0 && e.UpscoredChartIds.Count == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AlsoDeletesPlayerHistoryWhenHistoryIncluded()
    {
        var (handler, _, _, _, _, history, _) = BuildHandler();
        var userId = Guid.NewGuid();

        await handler.Handle(new WipeUserScoresCommand(userId, true), CancellationToken.None);

        history.Verify(p => p.DeleteHistoryForUser(userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
