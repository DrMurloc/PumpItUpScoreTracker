using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Commands;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Messages;
using ScoreTracker.ChartIntelligence.Application;
using ScoreTracker.ChartIntelligence.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Moq;
using ScoreTracker.Application.Queries;
using ScoreTracker.Application.Handlers;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class TierListSagaTests
{
    [Fact]
    public async Task ChartDifficultyUpdatedSavesNothingWhenNoChartsExist()
    {
        var tierLists = new Mock<ITierListRepository>();
        var saga = BuildSaga(tierLists: tierLists);

        await saga.Consume(BuildContext(new ChartDifficultyUpdatedEvent(ChartType.Single, 15)));

        tierLists.Verify(t => t.SaveEntry(It.IsAny<MixEnum>(), It.IsAny<SongTierListEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChartDifficultyUpdatedSkipsChartsThatHaveNoRating()
    {
        var ratedChart = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).Build();
        var unratedChart = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).Build();
        var charts = ChartsMockReturning(level: 15, type: ChartType.Single, new[] { ratedChart, unratedChart });
        var ratings = RatingsMockReturning(new[] { Rating(ratedChart.Id, difficulty: 15.5) });
        var tierLists = new Mock<ITierListRepository>();
        var saga = BuildSaga(charts: charts, chartRatings: ratings, tierLists: tierLists);

        await saga.Consume(BuildContext(new ChartDifficultyUpdatedEvent(ChartType.Single, 15)));

        tierLists.Verify(t => t.SaveEntry(MixEnum.Phoenix,
            It.Is<SongTierListEntry>(e => e.ChartId == ratedChart.Id), It.IsAny<CancellationToken>()),
            Times.Once);
        tierLists.Verify(t => t.SaveEntry(MixEnum.Phoenix,
            It.Is<SongTierListEntry>(e => e.ChartId == unratedChart.Id), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // diff = ratingDifficulty - chartLevel - 0.5; the saga's switch cascade puts diff
    // into buckets (-∞,-.75] → Overrated, (-.75,-.375] → VeryEasy, (-.375,-.125] → Easy,
    // (-.125,.125) → Medium, [.125,.375) → Hard, [.375,.75) → VeryHard, [.75,∞) → Underrated.
    // Inputs below land cleanly inside their bucket (chart level fixed at 15).
    [Theory]
    [InlineData(14.0, TierListCategory.Overrated)]   // diff = -1.5
    [InlineData(15.0, TierListCategory.VeryEasy)]    // diff = -0.5
    [InlineData(15.25, TierListCategory.Easy)]       // diff = -0.25
    [InlineData(15.5, TierListCategory.Medium)]      // diff = 0
    [InlineData(15.75, TierListCategory.Hard)]       // diff = 0.25
    [InlineData(16.0, TierListCategory.VeryHard)]    // diff = 0.5
    [InlineData(16.5, TierListCategory.Underrated)]  // diff = 1.0
    public async Task ChartDifficultyUpdatedAssignsCategoryFromDifficultyDelta(
        double ratingDifficulty, TierListCategory expected)
    {
        var chart = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).Build();
        var charts = ChartsMockReturning(level: 15, type: ChartType.Single, new[] { chart });
        var ratings = RatingsMockReturning(new[] { Rating(chart.Id, difficulty: ratingDifficulty) });
        var tierLists = new Mock<ITierListRepository>();
        var saga = BuildSaga(charts: charts, chartRatings: ratings, tierLists: tierLists);

        await saga.Consume(BuildContext(new ChartDifficultyUpdatedEvent(ChartType.Single, 15)));

        tierLists.Verify(t => t.SaveEntry(MixEnum.Phoenix,
            It.Is<SongTierListEntry>(e => e.ChartId == chart.Id && e.Category == expected),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChartDifficultyUpdatedAssignsContiguousAscendingOrderStartingAtZero()
    {
        var c1 = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).Build();
        var c2 = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).Build();
        var c3 = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).Build();
        var charts = ChartsMockReturning(level: 15, type: ChartType.Single, new[] { c1, c2, c3 });
        var ratings = RatingsMockReturning(new[]
        {
            Rating(c1.Id, difficulty: 15.5),
            Rating(c2.Id, difficulty: 15.5),
            Rating(c3.Id, difficulty: 15.5)
        });
        var saved = new List<SongTierListEntry>();
        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(t => t.SaveEntry(It.IsAny<MixEnum>(), It.IsAny<SongTierListEntry>(), It.IsAny<CancellationToken>()))
            .Callback<MixEnum, SongTierListEntry, CancellationToken>((_, e, _) => saved.Add(e));
        var saga = BuildSaga(charts: charts, chartRatings: ratings, tierLists: tierLists);

        await saga.Consume(BuildContext(new ChartDifficultyUpdatedEvent(ChartType.Single, 15)));

        Assert.Equal(new[] { 0, 1, 2 }, saved.Select(e => e.Order).ToArray());
    }

    [Fact]
    public async Task ChartDifficultyUpdatedSavesEntriesUnderDifficultyTierList()
    {
        var chart = new ChartBuilder().WithLevel(15).Build();
        var charts = ChartsMockReturning(level: 15, type: ChartType.Single, new[] { chart });
        var ratings = RatingsMockReturning(new[] { Rating(chart.Id, difficulty: 15.5) });
        var tierLists = new Mock<ITierListRepository>();
        var saga = BuildSaga(charts: charts, chartRatings: ratings, tierLists: tierLists);

        await saga.Consume(BuildContext(new ChartDifficultyUpdatedEvent(ChartType.Single, 15)));

        tierLists.Verify(t => t.SaveEntry(MixEnum.Phoenix,
            It.Is<SongTierListEntry>(e => (string)e.TierListName == "Difficulty"),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RelativeTierListReturnsEmptyWhenUserHasNoMatchingScores()
    {
        var charts = ChartsMockReturning(level: 15, type: ChartType.Single,
            new[] { new ChartBuilder().WithLevel(15).Build() });
        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetBestScores(MixEnum.Phoenix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
        var saga = BuildSaga(charts: charts, scores: scores);

        var result = await saga.Handle(
            new GetMyRelativeTierListQuery(ChartType.Single, DifficultyLevel.From(15), Guid.NewGuid()),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(15, "Scores")]
    [InlineData(23, "Scores")]
    [InlineData(24, "Official Scores")]
    [InlineData(28, "Official Scores")]
    public async Task RelativeTierListChoosesTierListNameByLevel(int level, string expectedListName)
    {
        var chart = new ChartBuilder().WithLevel(level).Build();
        var charts = ChartsMockReturning(level: level, type: ChartType.Single, new[] { chart });
        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetBestScores(MixEnum.Phoenix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordedPhoenixScore(chart.Id, 950000, PhoenixPlate.SuperbGame, false,
                    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
            });
        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(t => t.GetAllEntries(MixEnum.Phoenix, It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SongTierListEntry>());
        var saga = BuildSaga(charts: charts, scores: scores, tierLists: tierLists);

        await saga.Handle(
            new GetMyRelativeTierListQuery(ChartType.Single, DifficultyLevel.From(level), Guid.NewGuid()),
            CancellationToken.None);

        tierLists.Verify(t => t.GetAllEntries(MixEnum.Phoenix,
            It.Is<Name>(n => (string)n == expectedListName), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(10, true)]   // included: Range(10, 20) => 10..29, DifficultyLevel.Max
    [InlineData(27, true)]
    [InlineData(28, true)]
    [InlineData(29, true)]
    [InlineData(9, false)]   // excluded
    public async Task ProcessPassTierListIteratesLevelsTenThroughTwentyNineInclusive(int level, bool expected)
    {
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetPgUsers(MixEnum.Phoenix, It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(Guid UserId, Guid ChartId)>());
        scores.Setup(s => s.GetScores(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(), It.IsAny<ChartType>(),
                It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(t => t.GetUsersOnLevel(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel>(), It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(Array.Empty<Guid>());
        var saga = BuildSaga(charts: charts, scores: scores, tierLists: tierLists);

        await saga.Consume(BuildContext(new ProcessPassTierListCommand()));

        var times = expected ? Times.AtLeastOnce() : Times.Never();
        charts.Verify(c => c.GetCharts(MixEnum.Phoenix, DifficultyLevel.From(level), ChartType.Single,
            It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), times);
    }

    [Fact]
    public async Task PassTierListGroupsPhoenixPassersByDifficultyTitleNotCompetitiveLevel()
    {
        var passedTwiceFromThreeBelow = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var passedOnTheFolder = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var passedThreeAbove = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var passedOneAbove = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var passedOnlyOffTheTitles = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var titleSeventeen = Guid.NewGuid(); // three below the folder, weight 7
        var alsoTitleSeventeen = Guid.NewGuid();
        var titleTwenty = Guid.NewGuid(); // the folder's own title, weight 4
        var titleTwentyThree = Guid.NewGuid(); // three above, weight 3
        var titleTwentyOne = Guid.NewGuid(); // one above, weight 1
        var inNoTitleGroup = Guid.NewGuid();
        var charts = EmptyChartsMock();
        charts.Setup(c => c.GetCharts(MixEnum.Phoenix, DifficultyLevel.From(20), ChartType.Single,
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                passedTwiceFromThreeBelow, passedOnTheFolder, passedThreeAbove, passedOneAbove, passedOnlyOffTheTitles
            });
        var saved = new List<SongTierListEntry>();
        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(t => t.SaveEntries(MixEnum.Phoenix, It.IsAny<IEnumerable<SongTierListEntry>>(),
                It.IsAny<CancellationToken>()))
            .Callback<MixEnum, IEnumerable<SongTierListEntry>, CancellationToken>((_, entries, _) =>
                saved.AddRange(entries.Where(e => (string)e.TierListName == "Pass Count")))
            .Returns(Task.CompletedTask);
        void TitleGroup(int titleLevel, bool requireActive, params Guid[] players) =>
            tierLists.Setup(t => t.GetUsersOnLevel(MixEnum.Phoenix, DifficultyLevel.From(titleLevel),
                    It.IsAny<CancellationToken>(), requireActive))
                .ReturnsAsync(players);
        TitleGroup(17, true, titleSeventeen, alsoTitleSeventeen);
        TitleGroup(20, true, titleTwenty);
        TitleGroup(21, false, titleTwentyOne);
        TitleGroup(23, false, titleTwentyThree);
        var passes = new Dictionary<Guid, RecordedPhoenixScore[]>
        {
            [titleSeventeen] = new[] { Score(passedTwiceFromThreeBelow.Id, 900000) },
            [alsoTitleSeventeen] = new[] { Score(passedTwiceFromThreeBelow.Id, 910000) },
            [titleTwenty] = new[] { Score(passedOnTheFolder.Id, 900000) },
            [titleTwentyThree] = new[] { Score(passedThreeAbove.Id, 900000) },
            [titleTwentyOne] = new[]
            {
                Score(passedOneAbove.Id, 900000),
                new RecordedPhoenixScore(passedOnlyOffTheTitles.Id, PhoenixScore.From(700000), null, true,
                    DateTimeOffset.MinValue)
            },
            [inNoTitleGroup] = new[] { Score(passedOnlyOffTheTitles.Id, 900000) }
        };
        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetScores(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(), ChartType.Single,
                DifficultyLevel.From(20), DifficultyLevel.From(20), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, IEnumerable<Guid> ids, ChartType _, DifficultyLevel _, DifficultyLevel _,
                CancellationToken _) => ids.Where(passes.ContainsKey).SelectMany(id => passes[id]).ToArray());
        // The competitive-level reads would give the opposite answer: every passer is active, and
        // only the player in no title group sits near the folder.
        scores.Setup(s => s.GetScores(MixEnum.Phoenix, ChartType.Single, DifficultyLevel.From(20),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(passes.SelectMany(kv => kv.Value.Select(record => (kv.Key, record))).ToArray());
        scores.Setup(s => s.GetActiveUserIds(MixEnum.Phoenix, It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(passes.Keys.ToHashSet());
        var playerStats = new Mock<IPlayerStatsReader>();
        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(passes.Keys
                .Select(id => LevelStats(id, singles: id == inNoTitleGroup ? 20.5 : 5.0, doubles: 5.0)).ToArray());
        var saga = BuildSaga(charts: charts, scores: scores, tierLists: tierLists, playerStats: playerStats);

        await saga.Consume(BuildContext(new ProcessPassTierListCommand(MixEnum.Phoenix)));

        // 7 + 7, 4, 3 and 1; the broken attempt and the player in no title group add nothing.
        var sums = new Dictionary<Guid, int>
        {
            [passedTwiceFromThreeBelow.Id] = 14, [passedOnTheFolder.Id] = 4, [passedThreeAbove.Id] = 3,
            [passedOneAbove.Id] = 1, [passedOnlyOffTheTitles.Id] = 0
        };
        Assert.Equal(
            TierListProcessor.ProcessIntoTierList("Pass Count", sums).Select(e => (e.ChartId, e.Category, e.Order)),
            saved.Select(e => (e.ChartId, e.Category, e.Order)));
        Assert.Equal(
            new[]
            {
                passedOnlyOffTheTitles.Id, passedOneAbove.Id, passedThreeAbove.Id, passedOnTheFolder.Id,
                passedTwiceFromThreeBelow.Id
            },
            saved.OrderBy(e => e.Order).Select(e => e.ChartId).ToArray());
        Assert.Equal(TierListCategory.Unrecorded,
            saved.Single(e => e.ChartId == passedOnlyOffTheTitles.Id).Category);
        tierLists.Verify(t => t.GetUsersOnLevel(MixEnum.Phoenix, It.IsAny<DifficultyLevel>(),
            It.IsAny<CancellationToken>(), It.IsAny<bool>()), Times.AtLeastOnce);
        scores.Verify(s => s.GetActiveUserIds(It.IsAny<MixEnum>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
        scores.Verify(s => s.GetScores(MixEnum.Phoenix, It.Is<ChartType>(t => t != ChartType.CoOp),
            It.IsAny<DifficultyLevel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(MixEnum.Phoenix2)]
    [InlineData(MixEnum.Rise)]
    [InlineData(MixEnum.RiseArcade)]
    public async Task PassTierListNeverReadsDifficultyTitlesOnAMixWithoutThem(MixEnum mix)
    {
        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetActiveUserIds(mix, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());
        var tierLists = new Mock<ITierListRepository>();
        var saga = BuildSaga(scores: scores, tierLists: tierLists);

        await saga.Consume(BuildContext(new ProcessPassTierListCommand(mix)));

        tierLists.Verify(t => t.GetUsersOnLevel(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel>(),
            It.IsAny<CancellationToken>(), It.IsAny<bool>()), Times.Never);
        scores.Verify(s => s.GetScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<ChartType>(),
            It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(), It.IsAny<CancellationToken>()), Times.Never);
        scores.Verify(s => s.GetActiveUserIds(mix, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PassTierListRanksAPhoenix2FolderFromItsPlayersCompetitiveLevels()
    {
        var mostPassed = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var oncePassed = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var neverPassed = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var threeBelow = Guid.NewGuid(); // 17.4 → offset −3, weight 7
        var oneBelow = Guid.NewGuid(); // 19.2 → offset −1, weight 5
        var onTheFolder = Guid.NewGuid(); // 20.8 → offset 0, weight 4
        var threeAbove = Guid.NewGuid(); // 23.0 → offset +3, weight 3

        var saved = await RunPassTierList(MixEnum.Phoenix2, ChartType.Single, 20,
            new[] { mostPassed, oncePassed, neverPassed },
            new[]
            {
                (threeBelow, Score(mostPassed.Id, 900000)),
                (oneBelow, Score(mostPassed.Id, 900000)),
                (onTheFolder, Score(mostPassed.Id, 900000)),
                (threeAbove, Score(mostPassed.Id, 900000)),
                (onTheFolder, Score(oncePassed.Id, 850000))
            },
            new[]
            {
                LevelStats(threeBelow, singles: 17.4, doubles: 0),
                LevelStats(oneBelow, singles: 19.2, doubles: 0),
                LevelStats(onTheFolder, singles: 20.8, doubles: 0),
                LevelStats(threeAbove, singles: 23.0, doubles: 0)
            },
            activePlayers: new[] { threeBelow, oneBelow, onTheFolder, threeAbove });

        var byChart = saved.ToDictionary(e => e.ChartId);
        Assert.Equal(3, byChart.Count);
        Assert.NotEqual(TierListCategory.Unrecorded, byChart[mostPassed.Id].Category);
        Assert.NotEqual(TierListCategory.Unrecorded, byChart[oncePassed.Id].Category);
        Assert.Equal(TierListCategory.Unrecorded, byChart[neverPassed.Id].Category);
        // 7 + 5 + 4 + 3 = 19 against 4: the chart more players passed reads easier.
        Assert.True(byChart[mostPassed.Id].Category < byChart[oncePassed.Id].Category);
        Assert.True(byChart[mostPassed.Id].Order > byChart[oncePassed.Id].Order);
    }

    [Fact]
    public async Task PassTierListIgnoresBrokenAttempts()
    {
        var passed = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var broken = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var player = Guid.NewGuid();

        var saved = await RunPassTierList(MixEnum.Phoenix2, ChartType.Single, 20, new[] { passed, broken },
            new[]
            {
                (player, Score(passed.Id, 900000)),
                (player, new RecordedPhoenixScore(broken.Id, PhoenixScore.From(700000), null, true,
                    DateTimeOffset.MinValue))
            },
            new[] { LevelStats(player, singles: 20.5, doubles: 0) },
            activePlayers: new[] { player });

        var byChart = saved.ToDictionary(e => e.ChartId);
        Assert.NotEqual(TierListCategory.Unrecorded, byChart[passed.Id].Category);
        Assert.Equal(TierListCategory.Unrecorded, byChart[broken.Id].Category);
    }

    [Fact]
    public async Task PassTierListReadsTheDoublesLevelForAHalfDoubleFolder()
    {
        var doublesPlayersChart = new ChartBuilder().WithLevel(20).WithType(ChartType.HalfDouble).Build();
        var singlesPlayersChart = new ChartBuilder().WithLevel(20).WithType(ChartType.HalfDouble).Build();
        var doublesPlayer = Guid.NewGuid();
        var singlesPlayer = Guid.NewGuid();

        var saved = await RunPassTierList(MixEnum.Rise, ChartType.HalfDouble, 20,
            new[] { doublesPlayersChart, singlesPlayersChart },
            new[]
            {
                (doublesPlayer, Score(doublesPlayersChart.Id, 900000)),
                (singlesPlayer, Score(singlesPlayersChart.Id, 900000))
            },
            new[]
            {
                LevelStats(doublesPlayer, singles: 12.0, doubles: 20.5),
                LevelStats(singlesPlayer, singles: 20.5, doubles: 12.0)
            },
            activePlayers: new[] { doublesPlayer, singlesPlayer });

        var byChart = saved.ToDictionary(e => e.ChartId);
        Assert.NotEqual(TierListCategory.Unrecorded, byChart[doublesPlayersChart.Id].Category);
        Assert.Equal(TierListCategory.Unrecorded, byChart[singlesPlayersChart.Id].Category);
    }

    [Fact]
    public async Task PassTierListCountsInactivePlayersOnlyAboveTheFolder()
    {
        var passedBelow = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var passedAbove = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var inactiveBelow = Guid.NewGuid();
        var inactiveAbove = Guid.NewGuid();

        var saved = await RunPassTierList(MixEnum.Phoenix2, ChartType.Single, 20,
            new[] { passedBelow, passedAbove },
            new[]
            {
                (inactiveBelow, Score(passedBelow.Id, 900000)),
                (inactiveAbove, Score(passedAbove.Id, 900000))
            },
            new[]
            {
                LevelStats(inactiveBelow, singles: 19.5, doubles: 0),
                LevelStats(inactiveAbove, singles: 22.5, doubles: 0)
            },
            activePlayers: Array.Empty<Guid>());

        var byChart = saved.ToDictionary(e => e.ChartId);
        Assert.Equal(TierListCategory.Unrecorded, byChart[passedBelow.Id].Category);
        Assert.NotEqual(TierListCategory.Unrecorded, byChart[passedAbove.Id].Category);
    }

    [Fact]
    public async Task PassTierListSkipsAPasserWithNoStatsRow()
    {
        var passedWithoutStats = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var passedWithStats = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var noStats = Guid.NewGuid();
        var withStats = Guid.NewGuid();

        var saved = await RunPassTierList(MixEnum.Rise, ChartType.Single, 20,
            new[] { passedWithoutStats, passedWithStats },
            new[]
            {
                (noStats, Score(passedWithoutStats.Id, 900000)),
                (withStats, Score(passedWithStats.Id, 900000))
            },
            new[] { LevelStats(withStats, singles: 20.5, doubles: 0) },
            activePlayers: new[] { noStats, withStats });

        var byChart = saved.ToDictionary(e => e.ChartId);
        Assert.Equal(TierListCategory.Unrecorded, byChart[passedWithoutStats.Id].Category);
        Assert.NotEqual(TierListCategory.Unrecorded, byChart[passedWithStats.Id].Category);
    }

    [Fact]
    public async Task APgHolderWithNoStatsRowDoesNotStopThePassList()
    {
        var chart = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var noStats = Guid.NewGuid();
        var withStats = Guid.NewGuid();

        var saved = await RunPassTierList(MixEnum.Rise, ChartType.Single, 20,
            new[] { chart },
            new[] { (withStats, Score(chart.Id, 900000)) },
            new[] { LevelStats(withStats, singles: 20.5, doubles: 0) },
            activePlayers: new[] { withStats },
            pgHolders: new[] { (noStats, chart.Id) });

        Assert.NotEqual(TierListCategory.Unrecorded, Assert.Single(saved).Category);
    }

    [Fact]
    public async Task PassTierListLeavesAChartPassedOnlyByPlayersFourLevelsAboveUnrated()
    {
        var passedFromFarAbove = new ChartBuilder().WithLevel(10).WithType(ChartType.Single).Build();
        var passedOnTheFolder = new ChartBuilder().WithLevel(10).WithType(ChartType.Single).Build();
        var farAbove = Guid.NewGuid();
        var onTheFolder = Guid.NewGuid();

        var saved = await RunPassTierList(MixEnum.Phoenix2, ChartType.Single, 10,
            new[] { passedFromFarAbove, passedOnTheFolder },
            new[]
            {
                (farAbove, Score(passedFromFarAbove.Id, 990000)),
                (onTheFolder, Score(passedOnTheFolder.Id, 900000))
            },
            new[]
            {
                LevelStats(farAbove, singles: 14.5, doubles: 0),
                LevelStats(onTheFolder, singles: 10.5, doubles: 0)
            },
            activePlayers: new[] { farAbove, onTheFolder });

        var byChart = saved.ToDictionary(e => e.ChartId);
        Assert.Equal(TierListCategory.Unrecorded, byChart[passedFromFarAbove.Id].Category);
        Assert.NotEqual(TierListCategory.Unrecorded, byChart[passedOnTheFolder.Id].Category);
    }

    [Fact]
    public async Task PassTierListReadsActivityOnceAcrossTheLast120DaysOfTheClock()
    {
        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetActiveUserIds(MixEnum.Phoenix2, It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());
        var saga = BuildSaga(scores: scores);

        await saga.Consume(BuildContext(new ProcessPassTierListCommand(MixEnum.Phoenix2)));

        // BuildSaga's clock reads 2026-08-16; 120 days earlier is 2026-04-18.
        scores.Verify(s => s.GetActiveUserIds(MixEnum.Phoenix2,
            new DateTimeOffset(2026, 4, 18, 0, 0, 0, TimeSpan.Zero), It.IsAny<CancellationToken>()), Times.Once);
        scores.Verify(s => s.GetActiveUserIds(It.IsAny<MixEnum>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     Runs the Pass job for one mix with a single populated folder and returns the
    ///     "Pass Count" entries it saved. Every other folder is empty.
    /// </summary>
    private static async Task<IReadOnlyList<SongTierListEntry>> RunPassTierList(MixEnum mix, ChartType chartType,
        int level, IEnumerable<Chart> folderCharts,
        IEnumerable<(Guid UserId, RecordedPhoenixScore Record)> folderScores,
        IEnumerable<PlayerStatsRecord> stats, IEnumerable<Guid> activePlayers,
        IEnumerable<(Guid UserId, Guid ChartId)>? pgHolders = null)
    {
        var charts = EmptyChartsMock();
        charts.Setup(c => c.GetCharts(mix, DifficultyLevel.From(level), chartType,
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(folderCharts.ToArray());
        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetActiveUserIds(mix, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activePlayers.ToHashSet());
        scores.Setup(s => s.GetPgUsers(mix, chartType, DifficultyLevel.From(level), It.IsAny<CancellationToken>()))
            .ReturnsAsync((pgHolders ?? Array.Empty<(Guid UserId, Guid ChartId)>()).ToArray());
        scores.Setup(s => s.GetScores(mix, chartType, DifficultyLevel.From(level), It.IsAny<CancellationToken>()))
            .ReturnsAsync(folderScores.ToArray());
        var statsByUser = stats.ToDictionary(s => s.UserId);
        var playerStats = new Mock<IPlayerStatsReader>();
        playerStats.Setup(p => p.GetStats(mix, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, IEnumerable<Guid> ids, CancellationToken _) =>
                ids.Where(statsByUser.ContainsKey).Select(id => statsByUser[id]).ToArray());
        var saved = new List<SongTierListEntry>();
        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(t => t.SaveEntries(mix, It.IsAny<IEnumerable<SongTierListEntry>>(),
                It.IsAny<CancellationToken>()))
            .Callback<MixEnum, IEnumerable<SongTierListEntry>, CancellationToken>((_, entries, _) =>
                saved.AddRange(entries.Where(e => (string)e.TierListName == "Pass Count")))
            .Returns(Task.CompletedTask);
        var saga = BuildSaga(charts: charts, scores: scores, tierLists: tierLists, playerStats: playerStats);

        await saga.Consume(BuildContext(new ProcessPassTierListCommand(mix)));

        tierLists.Verify(t => t.SaveEntries(It.Is<MixEnum>(m => m != mix),
            It.IsAny<IEnumerable<SongTierListEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
        return saved;
    }

    private static TierListSaga BuildSaga(
        Mock<IChartDifficultyRatingRepository>? chartRatings = null,
        Mock<IChartRepository>? charts = null,
        Mock<ITierListRepository>? tierLists = null,
        Mock<IScoreReader>? scores = null,
        Mock<ICurrentUserAccessor>? currentUser = null,
        Mock<IPlayerStatsReader>? playerStats = null,
        Mock<IChartScoreStatsRepository>? chartStats = null,
        Mock<IFolderCohortStatsRepository>? cohortStats = null,
        Mock<ITitleRepository>? titles = null,
        Mock<IMediator>? mediator = null)
    {
        chartRatings ??= EmptyRatingsMock();
        charts ??= EmptyChartsMock();
        tierLists ??= new Mock<ITierListRepository>();
        scores ??= new Mock<IScoreReader>();
        currentUser ??= new Mock<ICurrentUserAccessor>();
        playerStats ??= new Mock<IPlayerStatsReader>();
        chartStats ??= new Mock<IChartScoreStatsRepository>();
        cohortStats ??= new Mock<IFolderCohortStatsRepository>();
        titles ??= new Mock<ITitleRepository>();
        return new TierListSaga(chartRatings.Object, charts.Object, tierLists.Object, scores.Object,
            currentUser.Object, playerStats.Object, new Mock<IChartScoringLevelRepository>().Object,
            chartStats.Object, cohortStats.Object, titles.Object,
            new Mock<IPumbilityPoolCompositionRepository>().Object, FakeDateTime.At(2026, 8, 16).Object,
            mediator?.Object ?? new Mock<IMediator>().Object);
    }

    private static Mock<IChartRepository> EmptyChartsMock()
    {
        var m = new Mock<IChartRepository>();
        m.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        return m;
    }

    private static Mock<IChartDifficultyRatingRepository> EmptyRatingsMock()
    {
        var m = new Mock<IChartDifficultyRatingRepository>();
        m.Setup(c => c.GetAllChartRatedDifficulties(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChartDifficultyRatingRecord>());
        return m;
    }

    private static Mock<IChartRepository> ChartsMockReturning(int level, ChartType type, IEnumerable<Chart> result)
    {
        var m = EmptyChartsMock();
        m.Setup(c => c.GetCharts(MixEnum.Phoenix, DifficultyLevel.From(level), type,
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return m;
    }

    private static Mock<IChartDifficultyRatingRepository> RatingsMockReturning(
        IEnumerable<ChartDifficultyRatingRecord> ratings)
    {
        var m = new Mock<IChartDifficultyRatingRepository>();
        m.Setup(c => c.GetAllChartRatedDifficulties(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ratings);
        return m;
    }

    private static ChartDifficultyRatingRecord Rating(Guid chartId, double difficulty) =>
        new(chartId, difficulty, RatingCount: 1, StandardDeviation: 0);

    private static ConsumeContext<T> BuildContext<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    // Characterization of the Scores tier list recompute (previously untested) ahead of the
    // rearchitecture — pins orchestration, not the bucketing math (that lives in
    // TierListSagaStaticsTests).

    [Fact]
    public async Task ProcessScoresTierListWeighsPlayersByStatsAndSavesUnderScoresTierList()
    {
        var chart = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var userId = Guid.NewGuid();
        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetScores(MixEnum.Phoenix, It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(Guid userId, RecordedPhoenixScore record)>());
        scores.Setup(s => s.GetScores(MixEnum.Phoenix, ChartType.Single, DifficultyLevel.From(20),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                (userId, new RecordedPhoenixScore(chart.Id, PhoenixScore.From(950000), PhoenixPlate.FairGame,
                    false, DateTimeOffset.MinValue))
            });
        var playerStats = new Mock<IPlayerStatsReader>();
        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, IEnumerable<Guid> ids, CancellationToken _) =>
                ids.Select(id => Stats(id, singlesCompetitive: 20.5)).ToArray());
        var tierLists = new Mock<ITierListRepository>();
        var saved = new List<SongTierListEntry>();
        tierLists.Setup(t => t.SaveEntries(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<SongTierListEntry>>(),
                It.IsAny<CancellationToken>()))
            .Callback<MixEnum, IEnumerable<SongTierListEntry>, CancellationToken>((_, e, _) => saved.AddRange(e))
            .Returns(Task.CompletedTask);
        var saga = BuildSaga(scores: scores, tierLists: tierLists, playerStats: playerStats);

        await saga.Consume(BuildContext(new ProcessScoresTiersListCommand()));

        var entry = Assert.Single(saved);
        Assert.Equal(chart.Id, entry.ChartId);
        Assert.Equal("Scores", (string)entry.TierListName);
        // Stats are fetched in ONE batch per folder; only the player's folder names them.
        playerStats.Verify(p => p.GetStats(MixEnum.Phoenix,
            It.Is<IEnumerable<Guid>>(ids => ids.Contains(userId)), It.IsAny<CancellationToken>()), Times.Once);
        // Levels 1-29 × {Single, Double} — one SaveEntries per folder, even when empty.
        tierLists.Verify(t => t.SaveEntries(MixEnum.Phoenix, It.IsAny<IEnumerable<SongTierListEntry>>(),
            It.IsAny<CancellationToken>()), Times.Exactly(58));
    }

    [Fact]
    public async Task ProcessScoresTierListPersistsVarianceOverComparablePlayersOnly()
    {
        var measured = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var singleScore = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var comparableA = Guid.NewGuid();
        var comparableB = Guid.NewGuid();
        var outOfBand = Guid.NewGuid();
        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetScores(MixEnum.Phoenix, It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(Guid userId, RecordedPhoenixScore record)>());
        scores.Setup(s => s.GetScores(MixEnum.Phoenix, ChartType.Single, DifficultyLevel.From(20),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                (comparableA, Score(measured.Id, 950000)),
                (comparableA, Score(singleScore.Id, 900000)),
                (comparableB, Score(measured.Id, 960000)),
                (outOfBand, Score(measured.Id, 999000))
            });
        var playerStats = new Mock<IPlayerStatsReader>();
        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, IEnumerable<Guid> ids, CancellationToken _) => ids.Select(id =>
                id == comparableA ? Stats(comparableA, singlesCompetitive: 20.5) :
                id == comparableB ? Stats(comparableB, singlesCompetitive: 19.0) :
                Stats(outOfBand, singlesCompetitive: 24.0)).ToArray());
        var chartStats = new Mock<IChartScoreStatsRepository>();
        var savedStats = new List<ChartScoreStatsRecord>();
        chartStats.Setup(c => c.SaveStats(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<ChartScoreStatsRecord>>(),
                It.IsAny<CancellationToken>()))
            .Callback<MixEnum, IEnumerable<ChartScoreStatsRecord>, CancellationToken>((_, s, _) =>
                savedStats.AddRange(s))
            .Returns(Task.CompletedTask);
        var saga = BuildSaga(scores: scores, playerStats: playerStats, chartStats: chartStats);

        await saga.Consume(BuildContext(new ProcessScoresTiersListCommand()));

        // The out-of-band player (comp 24 vs folder 20) is excluded, and the chart with a
        // single comparable score is not computable — only the two-score chart persists.
        var stat = Assert.Single(savedStats);
        Assert.Equal(measured.Id, stat.ChartId);
        Assert.Equal(2, stat.ScoreCount);
        // Sample stddev of {950000, 960000} = sqrt(50,000,000) ≈ 7071.068.
        Assert.Equal(7071.068, stat.ScoreStandardDeviation, precision: 3);
    }

    [Fact]
    public async Task ProcessScoresTierListPersistsFolderPassHistogramsByCompetitiveBucket()
    {
        var chart1 = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var chart2 = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var playerA = Guid.NewGuid(); // 20.5 → bucket 41, passes both
        var playerB = Guid.NewGuid(); // 20.5 → bucket 41, passes one (one broken attempt)
        var playerC = Guid.NewGuid(); // 19.0 → bucket 38, passes one
        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetScores(MixEnum.Phoenix, It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(Guid userId, RecordedPhoenixScore record)>());
        scores.Setup(s => s.GetScores(MixEnum.Phoenix, ChartType.Single, DifficultyLevel.From(20),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                (playerA, Score(chart1.Id, 950000)),
                (playerA, Score(chart2.Id, 940000)),
                (playerB, Score(chart1.Id, 930000)),
                (playerB, new RecordedPhoenixScore(chart2.Id, PhoenixScore.From(700000), null, true,
                    DateTimeOffset.MinValue)),
                (playerC, Score(chart1.Id, 920000))
            });
        var playerStats = new Mock<IPlayerStatsReader>();
        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, IEnumerable<Guid> ids, CancellationToken _) => ids.Select(id =>
                Stats(id, singlesCompetitive: id == playerC ? 19.0 : 20.5)).ToArray());
        var cohortStats = new Mock<IFolderCohortStatsRepository>();
        var savedBuckets = new List<FolderCohortBucketRecord>();
        cohortStats.Setup(c => c.SaveFolder(MixEnum.Phoenix, ChartType.Single, 20,
                It.IsAny<IEnumerable<FolderCohortBucketRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<MixEnum, ChartType, int, IEnumerable<FolderCohortBucketRecord>, CancellationToken>(
                (_, _, _, b, _) => savedBuckets.AddRange(b))
            .Returns(Task.CompletedTask);
        var saga = BuildSaga(scores: scores, playerStats: playerStats, cohortStats: cohortStats);

        await saga.Consume(BuildContext(new ProcessScoresTiersListCommand()));

        Assert.Equal(2, savedBuckets.Count);
        var midBucket = Assert.Single(savedBuckets, b => b.Bucket == 41);
        Assert.Equal(1, midBucket.PassHistogram[2]); // player A: 2 passes
        Assert.Equal(1, midBucket.PassHistogram[1]); // player B: 1 pass (broken attempt = 0)
        var lowBucket = Assert.Single(savedBuckets, b => b.Bucket == 38);
        Assert.Equal(1, lowBucket.PassHistogram[1]); // player C: 1 pass
    }

    [Fact]
    public async Task FolderCohortQueryMergesBucketsInsideTheSimilarPlayersWindow()
    {
        var cohortStats = new Mock<IFolderCohortStatsRepository>();
        cohortStats.Setup(c => c.GetBuckets(MixEnum.Phoenix, ChartType.Double, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new FolderCohortBucketRecord(40, new Dictionary<int, int> { { 5, 2 } }), // 20.0 — in window
                new FolderCohortBucketRecord(41, new Dictionary<int, int> { { 10, 1 }, { 20, 1 } }), // 20.5 — in window
                new FolderCohortBucketRecord(44, new Dictionary<int, int> { { 57, 3 } }) // 22.0 — outside ±0.5
            });
        var saga = BuildSaga(cohortStats: cohortStats);

        var result = await saga.Handle(
            new GetFolderCohortStatsQuery(MixEnum.Phoenix, ChartType.Double, 20, CompetitiveLevel: 20.3,
                PassCount: 10), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(4, result!.PlayerCount);
        Assert.Equal(10.0, result.AveragePasses); // (5 + 5 + 10 + 20) / 4
        Assert.Equal(0.75, result.PassPercentile); // 3 of 4 at or below 10 passes
    }

    [Fact]
    public async Task FolderCohortQueryReturnsNullWhenNoBucketsFallInTheWindow()
    {
        var cohortStats = new Mock<IFolderCohortStatsRepository>();
        cohortStats.Setup(c => c.GetBuckets(MixEnum.Phoenix, ChartType.Double, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new FolderCohortBucketRecord(50, new Dictionary<int, int> { { 3, 4 } }) });
        var saga = BuildSaga(cohortStats: cohortStats);

        var result = await saga.Handle(
            new GetFolderCohortStatsQuery(MixEnum.Phoenix, ChartType.Double, 20, CompetitiveLevel: 20.0,
                PassCount: 3), CancellationToken.None);

        Assert.Null(result);
    }

    private static RecordedPhoenixScore Score(Guid chartId, int score)
    {
        return new RecordedPhoenixScore(chartId, PhoenixScore.From(score), null, false, DateTimeOffset.MinValue);
    }

    private static PlayerStatsRecord Stats(Guid userId, double singlesCompetitive)
    {
        return new PlayerStatsRecord(userId, TotalRating: 0, HighestLevel: 1, ClearCount: 0,
            CoOpRating: 0, CoOpScore: 0, SkillRating: 0, SkillScore: 0, SkillLevel: 0,
            SinglesRating: 0, SinglesScore: 0, SinglesLevel: 0, DoublesRating: 0, DoublesScore: 0,
            DoublesLevel: 0, CompetitiveLevel: singlesCompetitive, SinglesCompetitiveLevel: singlesCompetitive,
            DoublesCompetitiveLevel: singlesCompetitive);
    }

    private static PlayerStatsRecord LevelStats(Guid userId, double singles, double doubles)
    {
        return Stats(userId, singles) with { SinglesCompetitiveLevel = singles, DoublesCompetitiveLevel = doubles };
    }
}
