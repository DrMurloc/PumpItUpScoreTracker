using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts.Messages;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PlayerHighlightPurgeConsumerTests
{
    [Fact]
    public async Task PurgesSummariesOlderThanThirtyDays()
    {
        var now = new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);
        var highlights = new Mock<IPlayerHighlightRepository>();
        var consumer = new PlayerHighlightPurgeConsumer(highlights.Object, FakeDateTime.At(now).Object);

        await consumer.Consume(Context(new PurgePlayerHighlightsCommand()));

        highlights.Verify(h => h.PurgeBefore(now.AddDays(-30), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }
}
