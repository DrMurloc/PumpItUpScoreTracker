using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class RenderShareCardHandlerTests
{
    [Fact]
    public async Task RendersTheRequestedCardThroughThePort()
    {
        var card = new TierListShareCard("Doubles 18", "Pass Difficulty", "Community", "#000000", "#000000",
            "#000000", "#000000", "#000000", "https://example.test/TierLists/Double/18", null,
            Array.Empty<TierListShareCard.Row>());
        var bytes = new byte[] { 1, 2, 3 };
        var renderer = new Mock<IShareCardRenderer>();
        renderer.Setup(r => r.RenderTierListCard(card, It.IsAny<CancellationToken>())).ReturnsAsync(bytes);
        var handler = new RenderShareCardHandler(renderer.Object);

        var result = await handler.Handle(new GetTierListShareCardQuery(card), CancellationToken.None);

        Assert.Same(bytes, result);
    }
}
