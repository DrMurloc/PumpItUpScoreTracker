using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts.Messages;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class CommunityHighlightPurgeConsumerTests
{
    [Fact]
    public async Task PurgesSummariesOlderThanThirtyDays()
    {
        var now = new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);
        var highlights = new Mock<ICommunityHighlightRepository>();
        var consumer = new CommunityHighlightPurgeConsumer(highlights.Object, FakeDateTime.At(now).Object);

        await consumer.Consume(Context(new PurgeCommunityHighlightsCommand()));

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
