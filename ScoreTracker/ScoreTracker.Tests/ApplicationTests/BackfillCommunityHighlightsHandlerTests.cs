using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class BackfillCommunityHighlightsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);

    private static ScoreHighlightsCapturedEvent Reconstructed() =>
        ScoreHighlightsCapturedEvent.Create(Now, Guid.NewGuid(), MixEnum.Phoenix, Guid.NewGuid(),
            Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>());

    [Fact]
    public async Task CapturesEachReconstructedEventForTheWindowAndReturnsTheCount()
    {
        var first = Reconstructed();
        var second = Reconstructed();
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetRecentHighlightEventsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second });
        var capturer = new Mock<ICommunityHighlightCapturer>();
        var handler = new BackfillCommunityHighlightsHandler(mediator.Object, capturer.Object,
            FakeDateTime.At(Now).Object);

        var count = await handler.Handle(new BackfillCommunityHighlightsCommand(7), CancellationToken.None);

        Assert.Equal(2, count);
        capturer.Verify(c => c.Capture(first, It.IsAny<CancellationToken>()), Times.Once);
        capturer.Verify(c => c.Capture(second, It.IsAny<CancellationToken>()), Times.Once);
        // The window is Now − 7 days.
        mediator.Verify(m => m.Send(It.Is<GetRecentHighlightEventsQuery>(q => q.Since == Now.AddDays(-7)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
