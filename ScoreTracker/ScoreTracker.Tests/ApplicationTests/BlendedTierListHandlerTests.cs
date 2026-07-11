using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.ChartIntelligence.Application;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class BlendedTierListHandlerTests
{
    [Fact]
    public async Task PopularityLensMapsCategoriesAndLeavesUnrankedChartsUnrecorded()
    {
        var ranked = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var unranked = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var charts = ChartsMock(new[] { ranked, unranked });
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Popularity",
            new[] { new SongTierListEntry("Popularity", ranked.Id, TierListCategory.Easy, 0) });
        var handler = BuildHandler(charts: charts, mediator: mediator);

        var result = await handler.Handle(Query("Popularity"), CancellationToken.None);

        Assert.Equal(TierListCategory.Easy, result.Entries.Single(e => e.ChartId == ranked.Id).Category);
        Assert.Equal(TierListCategory.Unrecorded, result.Entries.Single(e => e.ChartId == unranked.Id).Category);
        Assert.False(result.IsProvisionalFallback);
    }

    [Fact]
    public async Task ProvisionalFallbackFromAnySourcePropagates()
    {
        var chart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var charts = ChartsMock(new[] { chart });
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Popularity",
            new[] { new SongTierListEntry("Popularity", chart.Id, TierListCategory.Easy, 0) },
            isProvisional: true);
        var handler = BuildHandler(charts: charts, mediator: mediator);

        var result = await handler.Handle(Query("Popularity"), CancellationToken.None);

        Assert.True(result.IsProvisionalFallback);
    }

    [Fact]
    public async Task CommunityPassLensReadsNoPersonalData()
    {
        var chart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var charts = ChartsMock(new[] { chart });
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Pass Count",
            new[] { new SongTierListEntry("Pass Count", chart.Id, TierListCategory.Hard, 0) });
        var scores = new Mock<IScoreReader>();
        var userTierLists = new Mock<IUserTierListRepository>();
        var handler = BuildHandler(charts: charts, mediator: mediator, scores: scores,
            userTierLists: userTierLists);

        var result = await handler.Handle(Query("Pass", personalized: false), CancellationToken.None);

        Assert.Equal(TierListCategory.Hard, result.Entries.Single(e => e.ChartId == chart.Id).Category);
        scores.Verify(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        userTierLists.Verify(r => r.GetEntriesForCharts(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        mediator.Verify(m => m.Send(It.IsAny<GetMyRelativeTierListQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PersonalizedPassLensReadsSimilarPlayersFromMaterializedRowsWithoutFanOut()
    {
        var chart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var userId = Guid.NewGuid();
        var neighbor = Guid.NewGuid();
        var charts = ChartsMock(new[] { chart });
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Pass Count",
            new[] { new SongTierListEntry("Pass Count", chart.Id, TierListCategory.Medium, 0) });
        mediator.Setup(m => m.Send(It.IsAny<GetMyRelativeTierListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
                { new SongTierListEntry("My Relative Scores", chart.Id, TierListCategory.Medium, 0) });
        var titles = new Mock<ITitleRepository>();
        titles.Setup(t => t.GetCurrentTitleLevel(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DifficultyLevel.From(17));
        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(t => t.GetUsersOnLevel(MixEnum.Phoenix, It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new[] { neighbor });
        var userTierLists = new Mock<IUserTierListRepository>();
        userTierLists.Setup(r => r.GetEntriesForCharts(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new UserTierListEntryRecord(neighbor, chart.Id, TierListCategory.Easy, 0) });
        var handler = BuildHandler(charts: charts, mediator: mediator, titles: titles, tierLists: tierLists,
            userTierLists: userTierLists);

        var result = await handler.Handle(Query("Pass", personalized: true, userId: userId),
            CancellationToken.None);

        Assert.NotEqual(TierListCategory.Unrecorded, result.Entries.Single(e => e.ChartId == chart.Id).Category);
        userTierLists.Verify(r => r.GetEntriesForCharts(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        // The whole point of C1: exactly ONE relative-tier-list computation (the
        // requesting user's own) — never one per neighboring player.
        mediator.Verify(m => m.Send(It.IsAny<GetMyRelativeTierListQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UnknownLensThrows()
    {
        var handler = BuildHandler();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            handler.Handle(Query("Wombo Combo"), CancellationToken.None));
    }

    private static GetBlendedTierListQuery Query(string lens, bool personalized = false, Guid? userId = null)
    {
        return new GetBlendedTierListQuery(ChartType.Double, DifficultyLevel.From(17), lens, personalized, userId);
    }

    private static void SetupTierList(Mock<IMediator> mediator, string name,
        IEnumerable<SongTierListEntry> entries, bool isProvisional = false)
    {
        mediator.Setup(m => m.Send(It.IsAny<GetTierListWithFallbackQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierListResult(Array.Empty<SongTierListEntry>(), false));
        mediator.Setup(m => m.Send(It.Is<GetTierListWithFallbackQuery>(q => (string)q.TierListName == name),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierListResult(entries.ToArray(), isProvisional));
    }

    private static Mock<IChartRepository> ChartsMock(IEnumerable<Chart> charts)
    {
        var m = new Mock<IChartRepository>();
        m.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
        return m;
    }

    private static BlendedTierListHandler BuildHandler(
        Mock<IChartRepository>? charts = null,
        Mock<IMediator>? mediator = null,
        Mock<IScoreReader>? scores = null,
        Mock<ITitleRepository>? titles = null,
        Mock<ITierListRepository>? tierLists = null,
        Mock<IUserTierListRepository>? userTierLists = null)
    {
        charts ??= ChartsMock(Array.Empty<Chart>());
        mediator ??= new Mock<IMediator>();
        scores ??= new Mock<IScoreReader>();
        titles ??= new Mock<ITitleRepository>();
        tierLists ??= new Mock<ITierListRepository>();
        userTierLists ??= new Mock<IUserTierListRepository>();
        return new BlendedTierListHandler(mediator.Object, charts.Object, scores.Object, titles.Object,
            tierLists.Object, userTierLists.Object, new Mock<ICurrentUserAccessor>().Object,
            new MemoryCache(new MemoryCacheOptions()));
    }
}
