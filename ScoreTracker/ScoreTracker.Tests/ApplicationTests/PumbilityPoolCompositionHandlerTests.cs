using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.ChartIntelligence.Application;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PumbilityPoolCompositionHandlerTests
{
    private static readonly DateTimeOffset At = new(2026, 8, 16, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReadsTheMixOnceAndAnswersFromCacheAfterThat()
    {
        var record = new PumbilityPoolCompositionRecord(MixEnum.Phoenix2, At, 67, Array.Empty<PumbilityPoolBandRecord>());
        var repository = new Mock<IPumbilityPoolCompositionRepository>();
        repository.Setup(r => r.Get(MixEnum.Phoenix2, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        var handler = new PumbilityPoolCompositionHandler(repository.Object, new MemoryCache(new MemoryCacheOptions()));

        var first = await handler.Handle(new GetPumbilityPoolCompositionQuery(MixEnum.Phoenix2), CancellationToken.None);
        var second = await handler.Handle(new GetPumbilityPoolCompositionQuery(MixEnum.Phoenix2), CancellationToken.None);

        Assert.Same(record, first);
        Assert.Same(record, second);
        repository.Verify(r => r.Get(MixEnum.Phoenix2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EachMixIsItsOwnEntry()
    {
        var repository = new Mock<IPumbilityPoolCompositionRepository>();
        repository.Setup(r => r.Get(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum mix, CancellationToken _) =>
                new PumbilityPoolCompositionRecord(mix, At, 1, Array.Empty<PumbilityPoolBandRecord>()));
        var handler = new PumbilityPoolCompositionHandler(repository.Object, new MemoryCache(new MemoryCacheOptions()));

        var phoenix = await handler.Handle(new GetPumbilityPoolCompositionQuery(MixEnum.Phoenix), CancellationToken.None);
        var phoenix2 = await handler.Handle(new GetPumbilityPoolCompositionQuery(MixEnum.Phoenix2), CancellationToken.None);

        Assert.Equal(MixEnum.Phoenix, phoenix!.Mix);
        Assert.Equal(MixEnum.Phoenix2, phoenix2!.Mix);
    }

    [Fact]
    public async Task ANeverBuiltMixReadsAsNullAndIsAskedAgainSoon()
    {
        // Null is cached for a minute, not an hour: "not built yet" is the state the owner is about
        // to fix by pressing Rebuild, and the page should notice within a minute of that.
        var repository = new Mock<IPumbilityPoolCompositionRepository>();
        repository.Setup(r => r.Get(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PumbilityPoolCompositionRecord?)null);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new PumbilityPoolCompositionHandler(repository.Object, cache);

        Assert.Null(await handler.Handle(new GetPumbilityPoolCompositionQuery(MixEnum.Phoenix2), CancellationToken.None));

        // A cached null is still a cache hit — the repository is not hammered on every render.
        Assert.Null(await handler.Handle(new GetPumbilityPoolCompositionQuery(MixEnum.Phoenix2), CancellationToken.None));
        repository.Verify(r => r.Get(MixEnum.Phoenix2, It.IsAny<CancellationToken>()), Times.Once);
    }
}
