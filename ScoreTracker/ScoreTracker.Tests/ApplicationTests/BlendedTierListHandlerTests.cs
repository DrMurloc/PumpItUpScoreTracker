using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Application;
using ScoreTracker.Domain.Models;
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
    public async Task SkillSourcePoolsAdjacentFoldersWhenTheViewedFolderHasNoScores()
    {
        // The K7 cold-start fix: a player with zero scores in the viewed folder but a
        // history one level below still gets skill-derived estimates (deviations pool
        // across ±3 folders, decay-weighted). The old implementation returned nothing
        // here. Runs charts score above the player's folder baseline, twist charts
        // below — so the runs-heavy folder chart must land easier than the twisty one.
        var userId = Guid.NewGuid();
        var scored = new List<Chart>();
        var scores = new List<RecordedPhoenixScore>();
        var chips = new Dictionary<Guid, IReadOnlyList<ChartSkillChipRecord>>();
        for (var i = 0; i < 6; i++)
        {
            var runs = new ChartBuilder().WithLevel(16).WithType(ChartType.Double).Build();
            var twists = new ChartBuilder().WithLevel(16).WithType(ChartType.Double).Build();
            var jumps = new ChartBuilder().WithLevel(16).WithType(ChartType.Double).Build();
            scored.AddRange(new[] { runs, twists, jumps });
            scores.Add(new RecordedPhoenixScore(runs.Id, 960_000 + i * 1000, null, false, DateTimeOffset.MinValue));
            scores.Add(new RecordedPhoenixScore(twists.Id, 880_000 + i * 1000, null, false, DateTimeOffset.MinValue));
            scores.Add(new RecordedPhoenixScore(jumps.Id, 920_000 + i * 1000, null, false, DateTimeOffset.MinValue));
            chips[runs.Id] = new[] { new ChartSkillChipRecord(Skill.Runs, true, 0.8m) };
            chips[twists.Id] = new[] { new ChartSkillChipRecord(Skill.Twists, true, 0.8m) };
            chips[jumps.Id] = new[] { new ChartSkillChipRecord(Skill.Jumps, true, 0.8m) };
        }

        var runsFolderChart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var twistsFolderChart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        chips[runsFolderChart.Id] = new[] { new ChartSkillChipRecord(Skill.Runs, true, 0.8m) };
        chips[twistsFolderChart.Id] = new[] { new ChartSkillChipRecord(Skill.Twists, true, 0.8m) };

        var charts = ChartsMock(scored.Concat(new[] { runsFolderChart, twistsFolderChart }));
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Scores", Array.Empty<SongTierListEntry>());
        mediator.Setup(m => m.Send(It.IsAny<GetChartSkillChipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chips);
        mediator.Setup(m => m.Send(It.IsAny<GetMyRelativeTierListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SongTierListEntry>());
        var scoreReader = new Mock<IScoreReader>();
        scoreReader.Setup(s => s.GetBestScores(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scores);
        var titles = new Mock<ITitleRepository>();
        titles.Setup(t => t.GetCurrentTitleLevel(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DifficultyLevel.From(16));
        var handler = BuildHandler(charts: charts, mediator: mediator, scores: scoreReader, titles: titles);

        var result = await handler.Handle(Query("Score", personalized: true, userId: userId),
            CancellationToken.None);

        var runsEntry = result.Entries.Single(e => e.ChartId == runsFolderChart.Id);
        var twistsEntry = result.Entries.Single(e => e.ChartId == twistsFolderChart.Id);
        Assert.NotEqual(TierListCategory.Unrecorded, runsEntry.Category);
        Assert.NotEqual(TierListCategory.Unrecorded, twistsEntry.Category);
        Assert.True(runsEntry.Category < twistsEntry.Category,
            $"runs-heavy chart ({runsEntry.Category}) should rank easier than the twisty one ({twistsEntry.Category})");
    }

    [Fact]
    public async Task SkillSourceStaysSilentOnThinEvidence()
    {
        // One scored chart can't clear the evidence guard — the source must say
        // nothing rather than extrapolate from a single data point.
        var userId = Guid.NewGuid();
        var lone = new ChartBuilder().WithLevel(16).WithType(ChartType.Double).Build();
        var folderChart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var charts = ChartsMock(new[] { lone, folderChart });
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Scores", Array.Empty<SongTierListEntry>());
        mediator.Setup(m => m.Send(It.IsAny<GetChartSkillChipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<ChartSkillChipRecord>>
            {
                [lone.Id] = new[] { new ChartSkillChipRecord(Skill.Runs, true, 0.8m) },
                [folderChart.Id] = new[] { new ChartSkillChipRecord(Skill.Runs, true, 0.8m) }
            });
        mediator.Setup(m => m.Send(It.IsAny<GetMyRelativeTierListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SongTierListEntry>());
        var scoreReader = new Mock<IScoreReader>();
        scoreReader.Setup(s => s.GetBestScores(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
                { new RecordedPhoenixScore(lone.Id, 950_000, null, false, DateTimeOffset.MinValue) });
        var titles = new Mock<ITitleRepository>();
        titles.Setup(t => t.GetCurrentTitleLevel(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DifficultyLevel.From(16));
        var handler = BuildHandler(charts: charts, mediator: mediator, scores: scoreReader, titles: titles);

        var result = await handler.Handle(Query("Score", personalized: true, userId: userId),
            CancellationToken.None);

        Assert.Equal(TierListCategory.Unrecorded,
            result.Entries.Single(e => e.ChartId == folderChart.Id).Category);
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
        // Honors the level/type filters like the real repository — the handler asks
        // for the folder (level + type) AND the whole mix (the K7 ±3 window).
        var all = charts.ToArray();
        var m = new Mock<IChartRepository>();
        m.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, DifficultyLevel? level, ChartType? type, IEnumerable<Guid>? _,
                    CancellationToken _) =>
                all.Where(c => level == null || c.Level == level).Where(c => type == null || c.Type == type));
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
