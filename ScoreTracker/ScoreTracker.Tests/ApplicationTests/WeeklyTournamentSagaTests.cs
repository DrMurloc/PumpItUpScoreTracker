using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.WeeklyChallenge.Contracts;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;
using ScoreTracker.WeeklyChallenge.Contracts.Messages;
using ScoreTracker.WeeklyChallenge.Contracts.Commands;
using ScoreTracker.WeeklyChallenge.Application;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class WeeklyTournamentSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UpdateWeeklyChartsExitsEarlyWhenAnyCurrentWeekIsStillActive()
    {
        var weeklyTournies = new Mock<IWeeklyTournamentRepository>();
        weeklyTournies.Setup(w => w.GetWeeklyCharts(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                // Expiration date in the future relative to `Now` → week still active.
                new WeeklyTournamentChart(Guid.NewGuid(), Now.AddDays(2))
            });
        var saga = BuildSaga(weeklyTournies: weeklyTournies);

        await saga.Consume(BuildContext(new RotateWeeklyChartsCommand()));

        weeklyTournies.Verify(w => w.ClearTheBoard(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()), Times.Never);
        weeklyTournies.Verify(w => w.RegisterWeeklyChart(It.IsAny<MixEnum>(), It.IsAny<WeeklyTournamentChart>(),
            It.IsAny<CancellationToken>()), Times.Never);
        weeklyTournies.Verify(w => w.WriteHistories(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<UserTourneyHistory>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateWeeklyChartsSkipsRotationEntirelyWhenTheMixHasNoCharts()
    {
        // Parallel boards per mix: a rotation message for a mix with no chart catalog yet
        // (Phoenix 2 before its seed) no-ops without touching histories or the board.
        var weeklyTournies = new Mock<IWeeklyTournamentRepository>();
        weeklyTournies.Setup(w => w.GetWeeklyCharts(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WeeklyTournamentChart>());
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(MixEnum.Phoenix2, It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        var saga = BuildSaga(charts: charts, weeklyTournies: weeklyTournies);

        await saga.Consume(BuildContext(new RotateWeeklyChartsCommand(MixEnum.Phoenix2)));

        weeklyTournies.Verify(w => w.ClearTheBoard(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()), Times.Never);
        weeklyTournies.Verify(w => w.RegisterWeeklyChart(It.IsAny<MixEnum>(), It.IsAny<WeeklyTournamentChart>(),
            It.IsAny<CancellationToken>()), Times.Never);
        weeklyTournies.Verify(w => w.WriteHistories(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<UserTourneyHistory>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateWeeklyChartsWritesHistoriesAndClearsBoardWhenWeekExpired()
    {
        // Now is 2026-05-01 12:00 UTC; an expired chart (ExpirationDate < Now) lets the
        // chart-picker run end-to-end with a complete required-bucket fixture.
        var ctx = ExpiredWeekContext();
        var entryUser = Guid.NewGuid();
        var entryChart = Guid.NewGuid();
        ctx.WeeklyTournies.Setup(w => w.GetEntries(MixEnum.Phoenix, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new WeeklyTournamentEntry(entryUser, entryChart, 950000, PhoenixPlate.SuperbGame,
                    IsBroken: false, PhotoUrl: null, CompetitiveLevel: 20)
            });

        await ctx.Saga.Consume(BuildContext(new RotateWeeklyChartsCommand()));

        ctx.WeeklyTournies.Verify(w => w.WriteHistories(MixEnum.Phoenix,
            It.Is<IEnumerable<UserTourneyHistory>>(hs => hs.Any(h => h.UserId == entryUser
                                                                    && h.ChartId == entryChart)),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.WeeklyTournies.Verify(w => w.ClearTheBoard(MixEnum.Phoenix, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateWeeklyChartsRegistersExactlyOneChartPerMergedBucket()
    {
        // Required-bucket fixture has 8 charts; after merging CoOp 4-5 → 3,
        // S26 → S25, and D28+D29 → D27, only 3 buckets remain. Each gets one chart.
        var ctx = ExpiredWeekContext();
        // 05:00 UTC = midnight ET on the EST reference — the corrected Monday reset (was 03:00).
        var nextMondayReset = new DateTimeOffset(2026, 5, 4, 5, 0, 0, TimeSpan.Zero);

        await ctx.Saga.Consume(BuildContext(new RotateWeeklyChartsCommand()));

        ctx.WeeklyTournies.Verify(w => w.RegisterWeeklyChart(MixEnum.Phoenix,
            It.Is<WeeklyTournamentChart>(c => c.ExpirationDate == nextMondayReset),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task UpdateWeeklyChartsExcludesAlreadyPlayedChartsWhenPicking()
    {
        // Two CoOp 3 charts; one is in alreadyPlayed, so only the unplayed one can be picked.
        var ctx = ExpiredWeekContext();
        var unplayed = ctx.Charts["coop3"];
        var played = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(3).WithSongName("played").Build();
        ctx.GivenChartList(ReplaceCoOp3WithBoth(ctx.Charts, played));
        ctx.WeeklyTournies.Setup(w => w.GetAlreadyPlayedCharts(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { played.Id });

        await ctx.Saga.Consume(BuildContext(new RotateWeeklyChartsCommand()));

        ctx.WeeklyTournies.Verify(w => w.RegisterWeeklyChart(MixEnum.Phoenix,
            It.Is<WeeklyTournamentChart>(c => c.ChartId == unplayed.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.WeeklyTournies.Verify(w => w.RegisterWeeklyChart(It.IsAny<MixEnum>(),
            It.Is<WeeklyTournamentChart>(c => c.ChartId == played.Id),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateWeeklyChartsResetsAlreadyPlayedListWhenAllChartsInBucketAreExhausted()
    {
        // All 3 charts in the merged CoOp 3 bucket are in alreadyPlayed → no valid
        // candidates remain after filtering, so the algorithm clears the already-played
        // list for that bucket and falls back to picking from the full set.
        var ctx = ExpiredWeekContext();
        var coop3 = ctx.Charts["coop3"];
        var coop4 = ctx.Charts["coop4"];
        var coop5 = ctx.Charts["coop5"];
        ctx.WeeklyTournies.Setup(w => w.GetAlreadyPlayedCharts(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { coop3.Id, coop4.Id, coop5.Id });

        await ctx.Saga.Consume(BuildContext(new RotateWeeklyChartsCommand()));

        ctx.WeeklyTournies.Verify(w => w.ClearAlreadyPlayedCharts(MixEnum.Phoenix,
            It.Is<IEnumerable<Guid>>(ids => ids.Contains(coop3.Id) && ids.Contains(coop4.Id)
                                             && ids.Contains(coop5.Id)),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.WeeklyTournies.Verify(w => w.RegisterWeeklyChart(MixEnum.Phoenix,
            It.Is<WeeklyTournamentChart>(c => c.ChartId == coop3.Id || c.ChartId == coop4.Id
                                              || c.ChartId == coop5.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateWeeklyChartsMergesCoOpFourAndFiveIntoCoOpThreeBucket()
    {
        // Mark the CoOp 3 and CoOp 5 charts as already played. After the merge moves
        // levels 4 and 5 into the level-3 bucket, only the CoOp-4 chart is unplayed
        // — and the algorithm only iterates the merged (3, CoOp) bucket, so this
        // chart can ONLY be picked if the merge actually happened.
        var ctx = ExpiredWeekContext();
        var coop4 = ctx.Charts["coop4"];
        ctx.WeeklyTournies.Setup(w => w.GetAlreadyPlayedCharts(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ctx.Charts["coop3"].Id, ctx.Charts["coop5"].Id });

        await ctx.Saga.Consume(BuildContext(new RotateWeeklyChartsCommand()));

        ctx.WeeklyTournies.Verify(w => w.RegisterWeeklyChart(MixEnum.Phoenix,
            It.Is<WeeklyTournamentChart>(c => c.ChartId == coop4.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterWeeklyChartScoreSkipsWhenChartNotInCurrentWeek()
    {
        var someOtherChartId = Guid.NewGuid();
        var weeklyTournies = new Mock<IWeeklyTournamentRepository>();
        weeklyTournies.Setup(w => w.GetWeeklyCharts(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new WeeklyTournamentChart(someOtherChartId, Now.AddDays(2)) });
        var saga = BuildSaga(weeklyTournies: weeklyTournies);

        var requestedChartId = Guid.NewGuid();
        await saga.Handle(
            new RegisterWeeklyChartScoreCommand(Entry(requestedChartId, score: 950000)),
            CancellationToken.None);

        weeklyTournies.Verify(w => w.SaveEntry(It.IsAny<MixEnum>(), It.IsAny<WeeklyTournamentEntry>(),
            It.IsAny<ChallengeEntrySource>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterWeeklyChartScoreSavesEntryWithComputedCompetitiveLevelWhenNew()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext(chart);
        ctx.GivenStats(singlesCompetitive: 18.5, doublesCompetitive: 12.0);
        ctx.GivenNoExistingEntries(chart.Id);

        var userId = Guid.NewGuid();
        await ctx.Saga.Handle(
            new RegisterWeeklyChartScoreCommand(Entry(chart.Id, score: 950000, userId: userId)),
            CancellationToken.None);

        // Single chart → uses SinglesCompetitiveLevel.
        ctx.WeeklyTournies.Verify(w => w.SaveEntry(MixEnum.Phoenix,
            It.Is<WeeklyTournamentEntry>(e => e.UserId == userId && e.Score == (PhoenixScore)950000
                                              && e.CompetitiveLevel == 18.5),
            It.IsAny<ChallengeEntrySource>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(19.2, true)] // floor 19 sits in a S20's [19, 22] band
    [InlineData(18.5, false)] // floor 18 sits below it — sandbag-tier for this chart
    public async Task RegisterWeeklyChartScoreStampsTheBandVerdictOnTheEntry(
        double singlesCompetitive, bool expectedInRange)
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext(chart);
        ctx.GivenStats(singlesCompetitive: singlesCompetitive, doublesCompetitive: 12.0);
        ctx.GivenNoExistingEntries(chart.Id);

        await ctx.Saga.Handle(
            new RegisterWeeklyChartScoreCommand(Entry(chart.Id, score: 950000)),
            CancellationToken.None);

        ctx.WeeklyTournies.Verify(w => w.SaveEntry(MixEnum.Phoenix,
            It.IsAny<WeeklyTournamentEntry>(), It.IsAny<ChallengeEntrySource>(),
            expectedInRange, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterWeeklyChartScoreKeepsHigherExistingScoreWhenLowerSubmitted()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext(chart);
        ctx.GivenStats(singlesCompetitive: 20, doublesCompetitive: 20);
        ctx.GivenExistingEntries(chart.Id, new[]
        {
            Entry(chart.Id, score: 950000, userId: userId)
        });

        await ctx.Saga.Handle(
            new RegisterWeeklyChartScoreCommand(Entry(chart.Id, score: 800000, userId: userId)),
            CancellationToken.None);

        ctx.WeeklyTournies.Verify(w => w.SaveEntry(MixEnum.Phoenix,
            It.Is<WeeklyTournamentEntry>(e => e.UserId == userId && e.Score == (PhoenixScore)950000),
            It.IsAny<ChallengeEntrySource>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterWeeklyChartScoreReplacesScoreWhenHigherSubmitted()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext(chart);
        ctx.GivenStats(singlesCompetitive: 20, doublesCompetitive: 20);
        ctx.GivenExistingEntries(chart.Id, new[]
        {
            Entry(chart.Id, score: 800000, userId: userId)
        });

        await ctx.Saga.Handle(
            new RegisterWeeklyChartScoreCommand(Entry(chart.Id, score: 990000, userId: userId)),
            CancellationToken.None);

        ctx.WeeklyTournies.Verify(w => w.SaveEntry(MixEnum.Phoenix,
            It.Is<WeeklyTournamentEntry>(e => e.UserId == userId && e.Score == (PhoenixScore)990000),
            It.IsAny<ChallengeEntrySource>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterWeeklyChartScoreClearsBrokenWhenSubmissionIsUnbroken()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext(chart);
        ctx.GivenStats(singlesCompetitive: 20, doublesCompetitive: 20);
        ctx.GivenExistingEntries(chart.Id, new[]
        {
            Entry(chart.Id, score: 950000, userId: userId, isBroken: true)
        });

        await ctx.Saga.Handle(
            new RegisterWeeklyChartScoreCommand(Entry(chart.Id, score: 950000, userId: userId, isBroken: false)),
            CancellationToken.None);

        ctx.WeeklyTournies.Verify(w => w.SaveEntry(MixEnum.Phoenix,
            It.Is<WeeklyTournamentEntry>(e => e.UserId == userId && !e.IsBroken),
            It.IsAny<ChallengeEntrySource>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterWeeklyChartScorePublishesEventWhenPlaceChanges()
    {
        // No existing entry → existingPlace == null → place change → publish.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext(chart);
        ctx.GivenStats(singlesCompetitive: 20, doublesCompetitive: 20);
        ctx.GivenNoExistingEntries(chart.Id);
        ctx.Users.Setup(u => u.GetUser(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithId(userId).Build());

        await ctx.Saga.Handle(
            new RegisterWeeklyChartScoreCommand(Entry(chart.Id, score: 950000, userId: userId)),
            CancellationToken.None);

        ctx.Bus.Verify(b => b.Publish(
            It.Is<UserWeeklyChartsProgressedEvent>(e => e.UserId == userId && e.ChartId == chart.Id
                                                       && e.Score == 950000 && e.Place == 1
                                                       && e.Mix == MixEnum.Phoenix),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterWeeklyChartScoreDoesNotPublishWhenPlaceUnchanged()
    {
        // Solo player improving their own (already 1st) score keeps place = 1 → no publish.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext(chart);
        ctx.GivenStats(singlesCompetitive: 20, doublesCompetitive: 20);
        ctx.GivenExistingEntries(chart.Id, new[]
        {
            Entry(chart.Id, score: 900000, userId: userId)
        });

        await ctx.Saga.Handle(
            new RegisterWeeklyChartScoreCommand(Entry(chart.Id, score: 950000, userId: userId)),
            CancellationToken.None);

        ctx.Bus.Verify(b => b.Publish(It.IsAny<UserWeeklyChartsProgressedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScoreImportRegistersOnlyScoresOnTheCurrentWeeklyBoard()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var offBoardChartId = Guid.NewGuid();
        var ctx = new HandlerContext(chart);
        ctx.GivenStats(singlesCompetitive: 18.5, doublesCompetitive: 12.0);
        ctx.GivenNoExistingEntries(chart.Id);
        var userId = Guid.NewGuid();

        await ctx.Saga.Consume(BuildContext(ScoreImportCompletedEvent.Create(
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            ScoreImportCompletedEvent.OfficialImportSource, userId, MixEnum.Phoenix,
            new[]
            {
                new ScoreImportCompletedEvent.ImportedScore(chart.Id, 950000, "SuperbGame", false),
                new ScoreImportCompletedEvent.ImportedScore(offBoardChartId, 999000, "PerfectGame", false)
            })));

        ctx.WeeklyTournies.Verify(w => w.SaveEntry(MixEnum.Phoenix,
            It.Is<WeeklyTournamentEntry>(e => e.UserId == userId && e.ChartId == chart.Id
                                              && e.Score == (PhoenixScore)950000),
            It.IsAny<ChallengeEntrySource>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        ctx.WeeklyTournies.Verify(w => w.SaveEntry(It.IsAny<MixEnum>(),
            It.Is<WeeklyTournamentEntry>(e => e.ChartId == offBoardChartId),
            It.IsAny<ChallengeEntrySource>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScoreImportRegistersEntriesOntoTheImportingMixesBoard()
    {
        // Parallel boards per mix: a Phoenix 2 import consults the Phoenix 2 board and
        // writes its entry there — never onto the Phoenix board.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext(chart, MixEnum.Phoenix2);
        ctx.GivenStats(singlesCompetitive: 18.5, doublesCompetitive: 12.0);
        ctx.GivenNoExistingEntries(chart.Id);
        var userId = Guid.NewGuid();

        await ctx.Saga.Consume(BuildContext(ScoreImportCompletedEvent.Create(
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            ScoreImportCompletedEvent.OfficialImportSource, userId, MixEnum.Phoenix2,
            new[]
            {
                new ScoreImportCompletedEvent.ImportedScore(chart.Id, 950000, "SuperbGame", false)
            })));

        ctx.WeeklyTournies.Verify(w => w.SaveEntry(MixEnum.Phoenix2,
            It.Is<WeeklyTournamentEntry>(e => e.UserId == userId && e.ChartId == chart.Id
                                              && e.Score == (PhoenixScore)950000),
            It.IsAny<ChallengeEntrySource>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        ctx.WeeklyTournies.Verify(w => w.SaveEntry(MixEnum.Phoenix, It.IsAny<WeeklyTournamentEntry>(),
            It.IsAny<ChallengeEntrySource>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PlacementsQueryAnswersOnlyForChartsOnTheBoardWithAnEntry()
    {
        // The snapshot card's weekly read: the player's current place on whichever of
        // the batch's charts sit on this week's board — nothing for off-board charts,
        // nothing when the player has no entry.
        var weeklyChart = new ChartBuilder().WithType(ChartType.Double).WithLevel(21).Build();
        var offBoardChart = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext(weeklyChart);
        ctx.GivenExistingEntries(weeklyChart.Id, new[] { Entry(weeklyChart.Id, 970000, userId) });

        var placements = (await ctx.Saga.Handle(
            new GetUserWeeklyPlacementsQuery(userId, MixEnum.Phoenix, new[] { weeklyChart.Id, offBoardChart }),
            CancellationToken.None)).ToArray();

        var placement = Assert.Single(placements);
        Assert.Equal(weeklyChart.Id, placement.ChartId);
        Assert.Equal(1, placement.Place);
    }

    [Fact]
    public async Task PlacementsQueryReturnsNothingForPlayersOffTheBoard()
    {
        var weeklyChart = new ChartBuilder().WithType(ChartType.Double).WithLevel(21).Build();
        var ctx = new HandlerContext(weeklyChart);
        ctx.GivenExistingEntries(weeklyChart.Id, new[] { Entry(weeklyChart.Id, 990000) });

        var placements = await ctx.Saga.Handle(
            new GetUserWeeklyPlacementsQuery(Guid.NewGuid(), MixEnum.Phoenix, new[] { weeklyChart.Id }),
            CancellationToken.None);

        Assert.Empty(placements);
    }

    private sealed class HandlerContext
    {
        private readonly MixEnum _mix;
        public Mock<IChartRepository> Charts { get; } = new();
        public Mock<IWeeklyTournamentRepository> WeeklyTournies { get; } = new();
        public Mock<IPlayerStatsReader> PlayerStats { get; } = new();
        public Mock<IBotClient> Bot { get; } = new();
        public Mock<IUserReader> Users { get; } = new();
        public Mock<IBus> Bus { get; } = new();
        public WeeklyTournamentSaga Saga { get; }

        public HandlerContext(Chart chart, MixEnum mix = MixEnum.Phoenix)
        {
            _mix = mix;
            WeeklyTournies.Setup(w => w.GetWeeklyCharts(mix, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new WeeklyTournamentChart(chart.Id, Now.AddDays(2)) });
            Charts.Setup(c => c.GetCharts(mix, It.IsAny<DifficultyLevel?>(),
                    It.IsAny<ChartType?>(),
                    It.Is<IEnumerable<Guid>>(ids => ids != null),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { chart });

            Saga = BuildSaga(charts: Charts, weeklyTournies: WeeklyTournies, playerStats: PlayerStats,
                bus: Bus);
        }

        public void GivenStats(double singlesCompetitive, double doublesCompetitive)
        {
            PlayerStats.Setup(s => s.GetStats(_mix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlayerStatsRecord(Guid.NewGuid(), TotalRating: 0, HighestLevel: 1,
                    ClearCount: 0, CoOpRating: 0, CoOpScore: 0, SkillRating: 0, SkillScore: 0,
                    SkillLevel: 0, SinglesRating: 0, SinglesScore: 0, SinglesLevel: 0,
                    DoublesRating: 0, DoublesScore: 0, DoublesLevel: 0,
                    CompetitiveLevel: (singlesCompetitive + doublesCompetitive) / 2,
                    SinglesCompetitiveLevel: singlesCompetitive,
                    DoublesCompetitiveLevel: doublesCompetitive));
        }

        public void GivenNoExistingEntries(Guid chartId)
        {
            GivenExistingEntries(chartId, Array.Empty<WeeklyTournamentEntry>());
        }

        // Register reads the source-bearing view; the placements query reads the plain one —
        // stub both so either path sees the same board.
        public void GivenExistingEntries(Guid chartId, IEnumerable<WeeklyTournamentEntry> entries,
            ChallengeEntrySource source = ChallengeEntrySource.Official)
        {
            var frozen = entries.ToArray();
            WeeklyTournies.Setup(w => w.GetEntries(_mix, chartId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(frozen);
            WeeklyTournies.Setup(w => w.GetEntriesWithSources(_mix, chartId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(frozen.Select(e => (e, source)).ToArray());
        }
    }

    private static ChartPickerContext ExpiredWeekContext()
    {
        var ctx = new ChartPickerContext();
        // GetWeeklyCharts is checked twice (early-return guard, then reused). Return
        // an expired chart so the chart-picker proceeds.
        ctx.WeeklyTournies.Setup(w => w.GetWeeklyCharts(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new WeeklyTournamentChart(Guid.NewGuid(), Now.AddDays(-1))
            });
        ctx.WeeklyTournies.Setup(w => w.GetEntries(MixEnum.Phoenix, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WeeklyTournamentEntry>());
        ctx.WeeklyTournies.Setup(w => w.GetAlreadyPlayedCharts(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        ctx.GivenChartList(ctx.Charts.Values);
        return ctx;
    }

    private static IEnumerable<Chart> ReplaceCoOp3WithBoth(IDictionary<string, Chart> charts, Chart additional)
    {
        return charts.Values.Append(additional);
    }

    [Fact]
    public async Task WeeklyBoardRanksTopThreeAndFindsTheCallersRow()
    {
        var chartId = Guid.NewGuid();
        var me = Guid.NewGuid();
        var weeklyTournies = new Mock<IWeeklyTournamentRepository>();
        weeklyTournies.Setup(w => w.GetWeeklyCharts(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new WeeklyTournamentChart(chartId, Now.AddDays(3)) });
        WithLiveEntries(weeklyTournies, new[]
        {
            Entry(chartId, 990_000), Entry(chartId, 980_000), Entry(chartId, 970_000),
            Entry(chartId, 960_000, me), Entry(chartId, 950_000)
        });
        var saga = BuildSaga(weeklyTournies: weeklyTournies, users: UsersEcho());

        var view = await saga.Handle(new GetWeeklyBoardQuery(MixEnum.Phoenix, UserId: me),
            CancellationToken.None);

        Assert.True(view.IsLive);
        var summary = Assert.Single(view.Charts);
        Assert.Equal(5, summary.EntryCount);
        Assert.Equal(new[] { 1, 2, 3 }, summary.TopPlaces.Select(p => p.Place).ToArray());
        Assert.All(summary.TopPlaces, p => Assert.NotNull(p.Player));
        Assert.Equal(4, summary.MyRow!.Place);
        Assert.Equal(me, summary.MyRow.Entry.UserId);
    }

    [Fact]
    public async Task WeeklyBoardShipsBothLaddersSoTheRelevantSwitchRenumbers()
    {
        // A CL-24 player tops an S20 board (out of the [19, 22] band); two in-band players
        // trail. The overall ladder ranks all three; the in-range ladder drops the
        // sandbagger and renumbers, and every row says which world it belongs to.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var sandbagger = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var weeklyTournies = new Mock<IWeeklyTournamentRepository>();
        weeklyTournies.Setup(w => w.GetWeeklyCharts(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new WeeklyTournamentChart(chart.Id, Now.AddDays(3)) });
        WithLiveEntries(weeklyTournies, new[]
        {
            new WeeklyTournamentEntry(sandbagger, chart.Id, 995_000, PhoenixPlate.PerfectGame, false, null, 24.0),
            new WeeklyTournamentEntry(second, chart.Id, 970_000, PhoenixPlate.SuperbGame, false, null, 20.0),
            new WeeklyTournamentEntry(third, chart.Id, 960_000, PhoenixPlate.SuperbGame, false, null, 19.4)
        });
        var saga = BuildSaga(weeklyTournies: weeklyTournies, charts: ChartsReturning(new[] { chart }),
            users: UsersEcho());

        var view = await saga.Handle(new GetWeeklyBoardQuery(MixEnum.Phoenix), CancellationToken.None);

        var summary = Assert.Single(view.Charts);
        Assert.Equal(3, summary.EntryCount);
        Assert.Equal(2, summary.InRangeEntryCount);

        var topSandbagger = summary.TopPlaces.Single(r => r.Entry.UserId == sandbagger);
        Assert.False(topSandbagger.WasWithinRange);
        Assert.Null(topSandbagger.InRangePlace);
        var topSecond = summary.TopPlaces.Single(r => r.Entry.UserId == second);
        Assert.True(topSecond.WasWithinRange);
        Assert.Equal(2, topSecond.Place);
        Assert.Equal(1, topSecond.InRangePlace);

        Assert.Equal(new[] { second, third }, summary.InRangeTopPlaces.Select(r => r.Entry.UserId).ToArray());
        Assert.Equal(new[] { 1, 2 }, summary.InRangeTopPlaces.Select(r => r.Place).ToArray());
    }

    [Fact]
    public async Task WeeklyBoardComesBackInCanonicalOrderNotTheOrderItWasWritten()
    {
        // The week's rows arrive in whatever order they were drawn. The board reads top-down in
        // the Phoenix 1 order: hardest first, and within a level SINGLES before doubles; co-ops
        // last, the 2-player duet last of all.
        var s18 = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).WithSongName("s18").Build();
        var s21 = new ChartBuilder().WithType(ChartType.Single).WithLevel(21).WithSongName("s21").Build();
        var d21 = new ChartBuilder().WithType(ChartType.Double).WithLevel(21).WithSongName("d21").Build();
        var coOp5 = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(5).WithSongName("coop5").Build();
        var coOp2 = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(2).WithSongName("coop2").Build();
        var drawn = new[] { coOp2, s18, coOp5, d21, s21 };
        var weeklyTournies = new Mock<IWeeklyTournamentRepository>();
        weeklyTournies.Setup(w => w.GetWeeklyCharts(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(drawn.Select(c => new WeeklyTournamentChart(c.Id, Now.AddDays(3))).ToArray());
        WithLiveEntries(weeklyTournies, Array.Empty<WeeklyTournamentEntry>());
        var saga = BuildSaga(weeklyTournies: weeklyTournies, charts: ChartsReturning(drawn), users: UsersEcho());

        var view = await saga.Handle(new GetWeeklyBoardQuery(MixEnum.Phoenix), CancellationToken.None);

        // S21 before D21 (singles first within the 21s); then S18; then co-ops with the 5-player
        // ahead of the 2-player.
        Assert.Equal(new[] { s21.Id, d21.Id, s18.Id, coOp5.Id, coOp2.Id },
            view.Charts.Select(c => c.ChartId).ToArray());
    }

    [Fact]
    public async Task WeeklyBoardFlagsSuggestedChartsOnlyForCalibratedPlayers()
    {
        var inBand = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).WithSongName("in").Build();
        var outOfBand = new ChartBuilder().WithType(ChartType.Single).WithLevel(24).WithSongName("out").Build();
        var me = Guid.NewGuid();
        var weeklyTournies = new Mock<IWeeklyTournamentRepository>();
        weeklyTournies.Setup(w => w.GetWeeklyCharts(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new WeeklyTournamentChart(inBand.Id, Now.AddDays(3)),
                new WeeklyTournamentChart(outOfBand.Id, Now.AddDays(3))
            });
        WithLiveEntries(weeklyTournies, Array.Empty<WeeklyTournamentEntry>());
        var saga = BuildSaga(weeklyTournies: weeklyTournies, charts: ChartsReturning(new[] { inBand, outOfBand }),
            playerStats: StatsWith(singles: 18.0, doubles: 12.0), users: UsersEcho());

        var view = await saga.Handle(new GetWeeklyBoardQuery(MixEnum.Phoenix, UserId: me),
            CancellationToken.None);

        Assert.True(view.SuggestionsAvailable);
        Assert.True(view.Charts.Single(c => c.ChartId == inBand.Id).IsSuggested);
        Assert.False(view.Charts.Single(c => c.ChartId == outOfBand.Id).IsSuggested);
    }

    [Fact]
    public async Task WeeklyBoardSkipsSuggestionsWhenStatsAreUncalibrated()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).Build();
        var weeklyTournies = new Mock<IWeeklyTournamentRepository>();
        weeklyTournies.Setup(w => w.GetWeeklyCharts(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new WeeklyTournamentChart(chart.Id, Now.AddDays(3)) });
        WithLiveEntries(weeklyTournies, Array.Empty<WeeklyTournamentEntry>());
        var saga = BuildSaga(weeklyTournies: weeklyTournies, playerStats: StatsWith(singles: 8.0, doubles: 5.0),
            users: UsersEcho());

        var view = await saga.Handle(new GetWeeklyBoardQuery(MixEnum.Phoenix, UserId: Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(view.SuggestionsAvailable);
        Assert.False(view.Charts.Single().IsSuggested);
    }

    [Fact]
    public async Task WeeklyBoardScopesEveryChartToTheCommunityFilter()
    {
        var chartId = Guid.NewGuid();
        var member = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        var weeklyTournies = new Mock<IWeeklyTournamentRepository>();
        weeklyTournies.Setup(w => w.GetWeeklyCharts(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new WeeklyTournamentChart(chartId, Now.AddDays(3)) });
        WithLiveEntries(weeklyTournies, new[]
            { Entry(chartId, 990_000, outsider), Entry(chartId, 950_000, member) });
        var saga = BuildSaga(weeklyTournies: weeklyTournies, users: UsersEcho());

        var view = await saga.Handle(
            new GetWeeklyBoardQuery(MixEnum.Phoenix, OnlyUserIds: new[] { member }),
            CancellationToken.None);

        var summary = Assert.Single(view.Charts);
        Assert.Equal(1, summary.EntryCount);
        Assert.Equal(member, Assert.Single(summary.TopPlaces).Entry.UserId);
    }

    [Fact]
    public async Task WeeklyBoardReadsAFinishedWeekFromItsHistories()
    {
        var week = Now.AddDays(-14);
        var chartId = Guid.NewGuid();
        var weeklyTournies = new Mock<IWeeklyTournamentRepository>();
        weeklyTournies.Setup(w => w.GetPastEntries(MixEnum.Phoenix, week, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Entry(chartId, 990_000) });
        var saga = BuildSaga(weeklyTournies: weeklyTournies, users: UsersEcho());

        var view = await saga.Handle(new GetWeeklyBoardQuery(MixEnum.Phoenix, WeekStart: week),
            CancellationToken.None);

        Assert.False(view.IsLive);
        var summary = Assert.Single(view.Charts);
        Assert.Equal(chartId, summary.ChartId);
        Assert.Equal(week, summary.ExpirationDate);
        weeklyTournies.Verify(w => w.GetWeeklyCharts(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MonthlyTotalsPriceWithTheMixOwnPumbility()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var weeklyTournies = MonthlyRepo(pastDates: Array.Empty<DateTimeOffset>(),
            liveCharts: new[] { new WeeklyTournamentChart(chart.Id, Now.AddDays(3)) },
            liveEntries: new[] { Entry(chart.Id, 995_000) });
        var saga = BuildSaga(weeklyTournies: weeklyTournies, charts: ChartsReturning(new[] { chart }),
            users: UsersEcho());

        var view = await saga.Handle(new GetMonthlyLeaderboardQuery(MixEnum.Phoenix), CancellationToken.None);

        var row = Assert.Single(view.Rows);
        var expected = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, false)
            .GetScore(ChartType.Single, 20, 995_000, PhoenixPlate.SuperbGame);
        Assert.Equal(expected, row.Total);
        Assert.Equal(1, view.WeekInMonth);
        Assert.Equal(4, view.CountedPerPlayer);
    }

    [Fact]
    public async Task MonthlyBrokenPlaysPriceAtZero()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var weeklyTournies = MonthlyRepo(pastDates: Array.Empty<DateTimeOffset>(),
            liveCharts: new[] { new WeeklyTournamentChart(chart.Id, Now.AddDays(3)) },
            liveEntries: new[] { Entry(chart.Id, 995_000, isBroken: true) });
        var saga = BuildSaga(weeklyTournies: weeklyTournies, charts: ChartsReturning(new[] { chart }),
            users: UsersEcho());

        var view = await saga.Handle(new GetMonthlyLeaderboardQuery(MixEnum.Phoenix), CancellationToken.None);

        var row = Assert.Single(view.Rows);
        Assert.Equal(0, row.Total);
        Assert.Equal(0, Assert.Single(row.AllCounted).Points);
    }

    [Fact]
    public async Task MonthlyCombinedViewExcludesCoOpCharts()
    {
        var coOp = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(3).Build();
        var weeklyTournies = MonthlyRepo(pastDates: Array.Empty<DateTimeOffset>(),
            liveCharts: new[] { new WeeklyTournamentChart(coOp.Id, Now.AddDays(3)) },
            liveEntries: new[] { Entry(coOp.Id, 990_000) });
        var saga = BuildSaga(weeklyTournies: weeklyTournies, charts: ChartsReturning(new[] { coOp }),
            users: UsersEcho());

        var view = await saga.Handle(new GetMonthlyLeaderboardQuery(MixEnum.Phoenix), CancellationToken.None);

        Assert.Empty(view.Rows);
    }

    [Fact]
    public async Task MonthlyCoOpViewRanksByRawScore()
    {
        var coOp = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(3).Build();
        var higher = Guid.NewGuid();
        var lower = Guid.NewGuid();
        var weeklyTournies = MonthlyRepo(pastDates: Array.Empty<DateTimeOffset>(),
            liveCharts: new[] { new WeeklyTournamentChart(coOp.Id, Now.AddDays(3)) },
            liveEntries: new[] { Entry(coOp.Id, 900_000, lower), Entry(coOp.Id, 950_000, higher) });
        var saga = BuildSaga(weeklyTournies: weeklyTournies, charts: ChartsReturning(new[] { coOp }),
            users: UsersEcho());

        var view = await saga.Handle(new GetMonthlyLeaderboardQuery(MixEnum.Phoenix, Type: ChartType.CoOp),
            CancellationToken.None);

        Assert.Equal(new[] { higher, lower }, view.Rows.Select(r => r.Player!.Id).ToArray());
        Assert.Equal(950_000, view.Rows[0].Total);
    }

    [Fact]
    public async Task MonthlyCountsBestFourPerWeekOfTheWindow()
    {
        // Two finished weeks in the month plus the live board → 3 weeks, best 12 count.
        var now = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
        var me = Guid.NewGuid();
        var charts = Enumerable.Range(0, 14)
            .Select(i => new ChartBuilder().WithType(ChartType.Single).WithLevel(20).WithSongName($"c{i}").Build())
            .ToArray();
        var pastDates = new[]
        {
            new DateTimeOffset(2026, 5, 11, 5, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 18, 5, 0, 0, TimeSpan.Zero)
        };
        // Every entry the same grade on same-level charts → equal points each, so the counted
        // cap is the only thing shaping the total.
        var weeklyTournies = MonthlyRepo(pastDates,
            liveCharts: new[] { new WeeklyTournamentChart(charts[0].Id, now.AddDays(3)) },
            liveEntries: charts.Take(7).Select(c => Entry(c.Id, 995_000, me)).ToArray(),
            pastEntries: charts.Skip(7).Select(c => Entry(c.Id, 995_000, me)).ToArray());
        var saga = BuildSaga(weeklyTournies: weeklyTournies, charts: ChartsReturning(charts),
            users: UsersEcho(), dateTime: FakeDateTime.At(now));

        var view = await saga.Handle(new GetMonthlyLeaderboardQuery(MixEnum.Phoenix), CancellationToken.None);

        Assert.Equal(3, view.WeekInMonth);
        Assert.Equal(12, view.CountedPerPlayer);
        var row = Assert.Single(view.Rows);
        Assert.Equal(12, row.AllCounted.Count);
        Assert.Equal(4, row.TopFour.Count);
    }

    [Fact]
    public async Task MonthlyAttributesWeeksToTheMonthTheirBoardStartedIn()
    {
        // 2026-05-04's board ran Apr 27 – May 4: it belongs to April and must stay out of May.
        var now = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
        var aprilsWeek = new DateTimeOffset(2026, 5, 4, 5, 0, 0, TimeSpan.Zero);
        var maysWeek = new DateTimeOffset(2026, 5, 11, 5, 0, 0, TimeSpan.Zero);
        var weeklyTournies = MonthlyRepo(new[] { aprilsWeek, maysWeek },
            liveCharts: Array.Empty<WeeklyTournamentChart>(),
            liveEntries: Array.Empty<WeeklyTournamentEntry>());
        var saga = BuildSaga(weeklyTournies: weeklyTournies, users: UsersEcho(),
            dateTime: FakeDateTime.At(now));

        var view = await saga.Handle(new GetMonthlyLeaderboardQuery(MixEnum.Phoenix), CancellationToken.None);

        Assert.Equal(2, view.WeekInMonth);
        weeklyTournies.Verify(w => w.GetPastEntries(MixEnum.Phoenix,
            It.Is<IReadOnlyCollection<DateTimeOffset>>(dates => dates.Single() == maysWeek),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MonthlyTieBreaksEqualTotalsByRawScoreSum()
    {
        // Same grade band → identical PUMBILITY points; the higher raw score must rank first.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var bandFloor = (int)PhoenixLetterGrade.AA.GetMinimumScoreFor(MixEnum.Phoenix);
        var higherRaw = Guid.NewGuid();
        var lowerRaw = Guid.NewGuid();
        var weeklyTournies = MonthlyRepo(pastDates: Array.Empty<DateTimeOffset>(),
            liveCharts: new[] { new WeeklyTournamentChart(chart.Id, Now.AddDays(3)) },
            liveEntries: new[] { Entry(chart.Id, bandFloor, lowerRaw), Entry(chart.Id, bandFloor + 100, higherRaw) });
        var saga = BuildSaga(weeklyTournies: weeklyTournies, charts: ChartsReturning(new[] { chart }),
            users: UsersEcho());

        var view = await saga.Handle(new GetMonthlyLeaderboardQuery(MixEnum.Phoenix), CancellationToken.None);

        Assert.Equal(view.Rows[0].Total, view.Rows[1].Total);
        Assert.Equal(new[] { higherRaw, lowerRaw }, view.Rows.Select(r => r.Player!.Id).ToArray());
        Assert.Equal(new[] { 1, 2 }, view.Rows.Select(r => r.Place).ToArray());
    }

    [Fact]
    public async Task MonthlyPastMonthNeverReadsTheLiveBoard()
    {
        var now = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        // April's board: anchor 2026-04-13 started Apr 6.
        var anchor = new DateTimeOffset(2026, 4, 13, 5, 0, 0, TimeSpan.Zero);
        var aprilDates = new[] { anchor, new DateTimeOffset(2026, 4, 20, 5, 0, 0, TimeSpan.Zero) };
        var weeklyTournies = MonthlyRepo(aprilDates,
            liveCharts: Array.Empty<WeeklyTournamentChart>(),
            liveEntries: Array.Empty<WeeklyTournamentEntry>(),
            pastEntries: new[] { Entry(chart.Id, 995_000) });
        var saga = BuildSaga(weeklyTournies: weeklyTournies, charts: ChartsReturning(new[] { chart }),
            users: UsersEcho(), dateTime: FakeDateTime.At(now));

        var view = await saga.Handle(new GetMonthlyLeaderboardQuery(MixEnum.Phoenix, AnchorWeek: anchor),
            CancellationToken.None);

        Assert.Equal(2, view.WeekInMonth);
        Assert.Equal(new DateTimeOffset(2026, 4, 19, 5, 0, 0, TimeSpan.Zero), view.WindowEnd);
        weeklyTournies.Verify(w => w.GetEntries(It.IsAny<MixEnum>(), It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        weeklyTournies.Verify(w => w.GetWeeklyCharts(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WeeklyBoardRowsCarryTheirTrustSource()
    {
        var chartId = Guid.NewGuid();
        var verified = Guid.NewGuid();
        var selfReported = Guid.NewGuid();
        var weeklyTournies = new Mock<IWeeklyTournamentRepository>();
        weeklyTournies.Setup(w => w.GetWeeklyCharts(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new WeeklyTournamentChart(chartId, Now.AddDays(3)) });
        weeklyTournies.Setup(w => w.GetEntriesWithSources(MixEnum.Phoenix, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                (Entry(chartId, 990_000, verified), ChallengeEntrySource.Official),
                (Entry(chartId, 950_000, selfReported), ChallengeEntrySource.Manual)
            });
        var saga = BuildSaga(weeklyTournies: weeklyTournies, users: UsersEcho());

        var view = await saga.Handle(new GetWeeklyBoardQuery(MixEnum.Phoenix), CancellationToken.None);

        var rows = view.Charts.Single().TopPlaces;
        Assert.Equal(ChallengeEntrySource.Official, rows.Single(r => r.Entry.UserId == verified).Source);
        Assert.Equal(ChallengeEntrySource.Manual, rows.Single(r => r.Entry.UserId == selfReported).Source);
    }

    [Fact]
    public async Task RegisterStampsManualByDefaultAndOfficialFromTheImport()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext(chart);
        ctx.GivenStats(singlesCompetitive: 18.5, doublesCompetitive: 12.0);
        ctx.GivenNoExistingEntries(chart.Id);

        await ctx.Saga.Handle(new RegisterWeeklyChartScoreCommand(Entry(chart.Id, 900_000)),
            CancellationToken.None);
        await ctx.Saga.Consume(BuildContext(ScoreImportCompletedEvent.Create(
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            ScoreImportCompletedEvent.OfficialImportSource, Guid.NewGuid(), MixEnum.Phoenix,
            new[] { new ScoreImportCompletedEvent.ImportedScore(chart.Id, 950_000, "SuperbGame", false) })));

        ctx.WeeklyTournies.Verify(w => w.SaveEntry(MixEnum.Phoenix, It.IsAny<WeeklyTournamentEntry>(),
            ChallengeEntrySource.Manual, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        ctx.WeeklyTournies.Verify(w => w.SaveEntry(MixEnum.Phoenix, It.IsAny<WeeklyTournamentEntry>(),
            ChallengeEntrySource.Official, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterNeverDemotesAVerifiedScoreOrWipesItsPhoto()
    {
        // A weaker manual submit with no photo: the verified score keeps its tag AND its proof.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var userId = Guid.NewGuid();
        var photo = new Uri("https://images.example/proof.png");
        var ctx = new HandlerContext(chart);
        ctx.GivenStats(singlesCompetitive: 20, doublesCompetitive: 20);
        ctx.GivenExistingEntries(chart.Id,
            new[] { Entry(chart.Id, 950_000, userId) with { PhotoUrl = photo } });

        await ctx.Saga.Handle(
            new RegisterWeeklyChartScoreCommand(Entry(chart.Id, 900_000, userId)),
            CancellationToken.None);

        ctx.WeeklyTournies.Verify(w => w.SaveEntry(MixEnum.Phoenix,
            It.Is<WeeklyTournamentEntry>(e => e.Score == (PhoenixScore)950_000 && e.PhotoUrl == photo),
            ChallengeEntrySource.Official, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterTakesTheManualSourceWhenTheManualScoreWins()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext(chart);
        ctx.GivenStats(singlesCompetitive: 20, doublesCompetitive: 20);
        ctx.GivenExistingEntries(chart.Id, new[] { Entry(chart.Id, 900_000, userId) });

        await ctx.Saga.Handle(
            new RegisterWeeklyChartScoreCommand(Entry(chart.Id, 980_000, userId)),
            CancellationToken.None);

        ctx.WeeklyTournies.Verify(w => w.SaveEntry(MixEnum.Phoenix,
            It.Is<WeeklyTournamentEntry>(e => e.Score == (PhoenixScore)980_000),
            ChallengeEntrySource.Manual, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static void WithLiveEntries(Mock<IWeeklyTournamentRepository> mock,
        WeeklyTournamentEntry[] entries, ChallengeEntrySource source = ChallengeEntrySource.Official)
    {
        mock.Setup(w => w.GetEntries(MixEnum.Phoenix, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        mock.Setup(w => w.GetEntriesWithSources(MixEnum.Phoenix, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries.Select(e => (e, source)).ToArray());
    }

    [Fact]
    public async Task ChartBoardReturnsEveryRowRankedWithItsSource()
    {
        var chartId = Guid.NewGuid();
        var official = Guid.NewGuid();
        var selfReported = Guid.NewGuid();
        var weeklyTournies = new Mock<IWeeklyTournamentRepository>();
        weeklyTournies.Setup(w => w.GetEntriesWithSources(MixEnum.Phoenix, chartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                (Entry(chartId, 950_000, selfReported), ChallengeEntrySource.Manual),
                (Entry(chartId, 990_000, official), ChallengeEntrySource.Official)
            });
        var saga = BuildSaga(weeklyTournies: weeklyTournies, users: UsersEcho());

        var rows = await saga.Handle(new GetWeeklyChartBoardQuery(chartId, MixEnum.Phoenix), CancellationToken.None);

        Assert.Equal(new[] { 1, 2 }, rows.Select(r => r.Place).ToArray());
        Assert.Equal(official, rows[0].Entry.UserId);
        Assert.Equal(ChallengeEntrySource.Official, rows[0].Source);
        Assert.Equal(ChallengeEntrySource.Manual, rows[1].Source);
        Assert.All(rows, r => Assert.NotNull(r.Player));
    }

    // Display enrichment defaults to echoing whatever ids the handler asks for, so board tests
    // never pre-register players.
    private static Mock<IUserReader> UsersEcho(params User[] known)
    {
        var mock = new Mock<IUserReader>();
        mock.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                ids.Select(id => known.FirstOrDefault(k => k.Id == id)
                                 ?? new UserBuilder().WithId(id).Build()).ToArray());
        return mock;
    }

    private static Mock<IPlayerStatsReader> StatsWith(double singles, double doubles)
    {
        var mock = new Mock<IPlayerStatsReader>();
        mock.Setup(s => s.GetStats(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(Guid.NewGuid(), TotalRating: 0, HighestLevel: 1, ClearCount: 0,
                CoOpRating: 0, CoOpScore: 0, SkillRating: 0, SkillScore: 0, SkillLevel: 0, SinglesRating: 0,
                SinglesScore: 0, SinglesLevel: 0, DoublesRating: 0, DoublesScore: 0, DoublesLevel: 0,
                CompetitiveLevel: (singles + doubles) / 2, SinglesCompetitiveLevel: singles,
                DoublesCompetitiveLevel: doubles));
        return mock;
    }

    private static Mock<IChartRepository> ChartsReturning(IEnumerable<Chart> charts)
    {
        var mock = new Mock<IChartRepository>();
        mock.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
        return mock;
    }

    private static Mock<IWeeklyTournamentRepository> MonthlyRepo(
        IReadOnlyCollection<DateTimeOffset> pastDates,
        IReadOnlyCollection<WeeklyTournamentChart> liveCharts,
        IReadOnlyCollection<WeeklyTournamentEntry> liveEntries,
        IReadOnlyCollection<WeeklyTournamentEntry>? pastEntries = null)
    {
        var mock = new Mock<IWeeklyTournamentRepository>();
        mock.Setup(w => w.GetPastDates(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pastDates);
        mock.Setup(w => w.GetPastEntries(MixEnum.Phoenix, It.IsAny<IReadOnlyCollection<DateTimeOffset>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pastEntries ?? Array.Empty<WeeklyTournamentEntry>());
        mock.Setup(w => w.GetWeeklyCharts(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(liveCharts);
        mock.Setup(w => w.GetEntries(MixEnum.Phoenix, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(liveEntries);
        return mock;
    }

    private sealed class ChartPickerContext
    {
        public Mock<IChartRepository> Charts_ { get; } = new();
        public Mock<IWeeklyTournamentRepository> WeeklyTournies { get; } = new();
        public Mock<IPlayerStatsReader> PlayerStats { get; } = new();
        public Mock<IBotClient> Bot { get; } = new();
        public Mock<IUserReader> Users { get; } = new();
        public Mock<IBus> Bus { get; } = new();
        public Mock<IRandomNumberGenerator> Random { get; } = new();
        public IDictionary<string, Chart> Charts { get; }
        public WeeklyTournamentSaga Saga { get; private set; }

        public ChartPickerContext()
        {
            Charts = BuildRequiredChartFixture();
            Random.Setup(r => r.Next(It.IsAny<int>())).Returns(0);
            Saga = BuildSaga(charts: Charts_, weeklyTournies: WeeklyTournies, playerStats: PlayerStats,
                bus: Bus, random: Random);
        }

        public void GivenChartList(IEnumerable<Chart> charts)
        {
            Charts_.Setup(c => c.GetCharts(MixEnum.Phoenix, It.IsAny<DifficultyLevel?>(),
                    It.IsAny<ChartType?>(),
                    It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(charts);
        }

        // The chart-picker hard-codes lookups for these (level, type) buckets; a mix
        // missing one simply skips that merge now (per-mix rotation robustness).
        private static IDictionary<string, Chart> BuildRequiredChartFixture() =>
            new Dictionary<string, Chart>
            {
                ["coop3"] = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(3).WithSongName("c3").Build(),
                ["coop4"] = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(4).WithSongName("c4").Build(),
                ["coop5"] = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(5).WithSongName("c5").Build(),
                ["single25"] = new ChartBuilder().WithType(ChartType.Single).WithLevel(25).WithSongName("s25").Build(),
                ["single26"] = new ChartBuilder().WithType(ChartType.Single).WithLevel(26).WithSongName("s26").Build(),
                ["double27"] = new ChartBuilder().WithType(ChartType.Double).WithLevel(27).WithSongName("d27").Build(),
                ["double28"] = new ChartBuilder().WithType(ChartType.Double).WithLevel(28).WithSongName("d28").Build(),
                ["double29"] = new ChartBuilder().WithType(ChartType.Double).WithLevel(29).WithSongName("d29").Build()
            };
    }

    private static WeeklyTournamentSaga BuildSaga(
        Mock<IChartRepository>? charts = null,
        Mock<IWeeklyTournamentRepository>? weeklyTournies = null,
        Mock<IPlayerStatsReader>? playerStats = null,
        Mock<IUserReader>? users = null,
        Mock<IBus>? bus = null,
        Mock<IDateTimeOffsetAccessor>? dateTime = null,
        Mock<IRandomNumberGenerator>? random = null)
    {
        if (charts == null)
        {
            // The board read fetches the catalog unconditionally now (the in-range ladder);
            // a bare context answers with an empty catalog, which filters nothing.
            charts = new Mock<IChartRepository>();
            charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(),
                    It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Chart>());
        }

        weeklyTournies ??= new Mock<IWeeklyTournamentRepository>();
        playerStats ??= new Mock<IPlayerStatsReader>();
        users ??= new Mock<IUserReader>();
        bus ??= new Mock<IBus>();
        dateTime ??= FakeDateTime.At(Now);
        random ??= new Mock<IRandomNumberGenerator>();
        return new WeeklyTournamentSaga(charts.Object, weeklyTournies.Object, playerStats.Object,
            NullLogger<WeeklyTournamentSaga>.Instance, users.Object, bus.Object,
            dateTime.Object, random.Object, new MemoryCache(new MemoryCacheOptions()));
    }

    private static WeeklyTournamentEntry Entry(Guid chartId, int score, Guid? userId = null,
        bool isBroken = false) =>
        new(UserId: userId ?? Guid.NewGuid(), ChartId: chartId, Score: score,
            Plate: PhoenixPlate.SuperbGame, IsBroken: isBroken, PhotoUrl: null,
            CompetitiveLevel: 20);

    private static ConsumeContext<T> BuildContext<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }
}
