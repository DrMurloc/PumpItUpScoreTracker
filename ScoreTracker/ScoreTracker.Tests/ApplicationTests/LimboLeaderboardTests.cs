using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class LimboLeaderboardTests
{
    private static readonly Guid Gargoyle = Guid.NewGuid();
    private static readonly Guid Unflagged = Guid.NewGuid();

    [Fact]
    public async Task FlaggedChartsAreReadOnceAndThenServedFromCache()
    {
        var charts = new Mock<ILimboChartRepository>();
        charts.Setup(c => c.GetLimboCharts(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { Gargoyle });
        var handler = new GetLimboChartsHandler(charts.Object, NewCache());

        var first = await handler.Handle(new GetLimboChartsQuery(MixEnum.Phoenix2), CancellationToken.None);
        var second = await handler.Handle(new GetLimboChartsQuery(MixEnum.Phoenix2), CancellationToken.None);

        Assert.Contains(Gargoyle, first);
        Assert.Contains(Gargoyle, second);
        charts.Verify(c => c.GetLimboCharts(MixEnum.Phoenix2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EachMixKeepsItsOwnFlagSet()
    {
        var charts = new Mock<ILimboChartRepository>();
        charts.Setup(c => c.GetLimboCharts(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { Gargoyle });
        charts.Setup(c => c.GetLimboCharts(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());
        var handler = new GetLimboChartsHandler(charts.Object, NewCache());

        Assert.Contains(Gargoyle,
            await handler.Handle(new GetLimboChartsQuery(MixEnum.Phoenix2), CancellationToken.None));
        // The same chart id exists on both mixes; flagging it on one must not light it on the other.
        Assert.Empty(await handler.Handle(new GetLimboChartsQuery(MixEnum.Phoenix), CancellationToken.None));
    }

    [Fact]
    public async Task AnUnflaggedChartHasNoBoardAndIsNeverQueried()
    {
        var journal = new Mock<IScoreJournalRepository>();
        var handler = new GetLowestPassingScoresHandler(journal.Object, MediatorReturning(Gargoyle), NewCache());

        var board = await handler.Handle(new GetLowestPassingScoresQuery(Unflagged, MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.Empty(board);
        journal.Verify(
            j => j.GetLowestPassingPlays(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AFlaggedChartServesItsBoardAndCachesIt()
    {
        var journal = new Mock<IScoreJournalRepository>();
        journal.Setup(j => j.GetLowestPassingPlays(MixEnum.Phoenix2, Gargoyle, It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Row("SCARFACE", 312_004) });
        var handler = new GetLowestPassingScoresHandler(journal.Object, MediatorReturning(Gargoyle), NewCache());
        var query = new GetLowestPassingScoresQuery(Gargoyle, MixEnum.Phoenix2);

        var first = await handler.Handle(query, CancellationToken.None);
        var second = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(312_004, (int)first.Single().Score);
        Assert.Equal(312_004, (int)second.Single().Score);
        journal.Verify(
            j => j.GetLowestPassingPlays(MixEnum.Phoenix2, Gargoyle, It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnObservedPlayEvictsItsChartsBoard()
    {
        var cache = NewCache();
        var key = LedgerCacheKeys.LimboBoard(MixEnum.Phoenix2, Gargoyle);
        cache.Set(key, new[] { Row("STALE", 999_999) });

        var handler = new RecordObservedPlaysHandler(Mock.Of<IScoreJournalRepository>(), cache);
        await handler.Handle(new RecordObservedPlaysCommand(Guid.NewGuid(), MixEnum.Phoenix2, "officialImport",
            Guid.NewGuid(), new[] { Play(Gargoyle, 312_004) }), CancellationToken.None);

        Assert.False(cache.TryGetValue(key, out _));
    }

    [Fact]
    public async Task AnObservedPlayLeavesOtherChartsAlone()
    {
        var cache = NewCache();
        var untouched = LedgerCacheKeys.LimboBoard(MixEnum.Phoenix2, Unflagged);
        cache.Set(untouched, new[] { Row("KEEP", 180_000) });

        var handler = new RecordObservedPlaysHandler(Mock.Of<IScoreJournalRepository>(), cache);
        await handler.Handle(new RecordObservedPlaysCommand(Guid.NewGuid(), MixEnum.Phoenix2, "officialImport",
            Guid.NewGuid(), new[] { Play(Gargoyle, 312_004) }), CancellationToken.None);

        Assert.True(cache.TryGetValue(untouched, out _));
    }

    [Fact]
    public async Task AWalkOffEvictsNothingBecauseItWasNeverStored()
    {
        var cache = NewCache();
        var key = LedgerCacheKeys.LimboBoard(MixEnum.Phoenix2, Gargoyle);
        cache.Set(key, new[] { Row("KEEP", 312_004) });

        var handler = new RecordObservedPlaysHandler(Mock.Of<IScoreJournalRepository>(), cache);
        // Broken, nothing judged: never journaled (score-truth-model D7), so the board did not move.
        await handler.Handle(new RecordObservedPlaysCommand(Guid.NewGuid(), MixEnum.Phoenix2, "officialImport",
                Guid.NewGuid(),
                new[]
                {
                    new RecordObservedPlaysCommand.ObservedPlay(Gargoyle, 0, null, true,
                        new DateTimeOffset(2026, 8, 9, 0, 0, 0, TimeSpan.Zero), new JudgementCounts(0, 0, 0, 0, 0))
                }),
            CancellationToken.None);

        Assert.True(cache.TryGetValue(key, out _));
    }

    private static MemoryCache NewCache()
    {
        return new MemoryCache(new MemoryCacheOptions());
    }

    private static IMediator MediatorReturning(params Guid[] flagged)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetLimboChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<Guid>)flagged.ToHashSet());
        return mediator.Object;
    }

    private static UserPhoenixScore Row(string name, int score)
    {
        return new UserPhoenixScore(Guid.NewGuid(), Gargoyle, name, score, null, false);
    }

    private static RecordObservedPlaysCommand.ObservedPlay Play(Guid chartId, int score)
    {
        return new RecordObservedPlaysCommand.ObservedPlay(chartId, score, null, false,
            new DateTimeOffset(2026, 8, 9, 0, 0, 0, TimeSpan.Zero), new JudgementCounts(10, 5, 3, 2, 160));
    }
}
