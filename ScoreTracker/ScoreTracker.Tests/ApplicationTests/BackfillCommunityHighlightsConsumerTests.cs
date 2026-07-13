using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Moq;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts.Messages;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class BackfillCommunityHighlightsConsumerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);

    private static ScoreHighlightsCapturedEvent Reconstructed() =>
        ScoreHighlightsCapturedEvent.Create(Now, Guid.NewGuid(), MixEnum.Phoenix, Guid.NewGuid(),
            Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>());

    [Fact]
    public async Task CapturesEachReconstructedEventForTheWindow()
    {
        var first = Reconstructed();
        var second = Reconstructed();
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetRecentHighlightEventsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second });
        var capturer = new Mock<ICommunityHighlightCapturer>();
        var consumer = new BackfillCommunityHighlightsConsumer(mediator.Object, capturer.Object,
            FakeDateTime.At(Now).Object);

        var ctx = new Mock<ConsumeContext<BackfillCommunityHighlightsCommand>>();
        ctx.SetupGet(c => c.Message).Returns(new BackfillCommunityHighlightsCommand(7));
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(ctx.Object);

        capturer.Verify(c => c.Capture(first, It.IsAny<CancellationToken>()), Times.Once);
        capturer.Verify(c => c.Capture(second, It.IsAny<CancellationToken>()), Times.Once);
        // The window is Now − 7 days.
        mediator.Verify(m => m.Send(It.Is<GetRecentHighlightEventsQuery>(q => q.Since == Now.AddDays(-7)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
