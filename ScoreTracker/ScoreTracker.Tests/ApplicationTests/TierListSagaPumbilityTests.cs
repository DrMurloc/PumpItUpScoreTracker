using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.ChartIntelligence.Application;
using ScoreTracker.ChartIntelligence.Contracts.Messages;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     TierListSaga's PUMBILITY rebuild (its ProcessPumbilityTierListCommand consumer) — a
///     sibling file to TierListSagaTests, the same split TierListSagaStaticsTests already uses.
/// </summary>
public sealed class TierListSagaPumbilityTests
{
    private static readonly DateTimeOffset Recorded = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ChartsInMorePoolsBandBetterThanChartsInFewer()
    {
        var everyone = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();
        var popular = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var niche = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var filler = Enumerable.Range(0, 6)
            .Select(_ => new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build()).ToArray();

        // Everyone SSS+s the popular chart and the filler; one player scrapes the niche one.
        var scores = new List<(Guid, RecordedPhoenixScore)>();
        foreach (var user in everyone)
        {
            scores.Add((user, Score(popular, 995_000)));
            foreach (var chart in filler) scores.Add((user, Score(chart, 970_000)));
        }

        scores.Add((everyone[0], Score(niche, 960_000)));

        var charts = filler.Concat(new[] { popular, niche }).ToList();
        GiveFullPools(charts, scores, everyone);
        var saved = new List<SavedFolder>();
        var saga = BuildSaga(charts, scores, saved);

        await saga.Consume(Context(new ProcessPumbilityTierListCommand()));

        var community = saved.Single(f => f.Level == 20 && f.ChartType == ChartType.Single)
            .ByCohort[PumbilityCohortKeys.Community].Entries;
        var popularEntry = community.Single(e => e.ChartId == popular.Id);
        var nicheEntry = community.Single(e => e.ChartId == niche.Id);

        Assert.Equal(8, popularEntry.Appearances);
        Assert.Equal(1, nicheEntry.Appearances);
        // The category enum runs easiest first, so more pools must mean a lower value.
        Assert.True(popularEntry.Category < nicheEntry.Category,
            $"{popularEntry.Appearances} pools came out {popularEntry.Category}, " +
            $"{nicheEntry.Appearances} came out {nicheEntry.Category}");
    }

    [Fact]
    public async Task ACohortCountsOnlyItsOwnMembers()
    {
        var titled = Guid.NewGuid();
        var untitled = Guid.NewGuid();
        var chart = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var scores = new List<(Guid, RecordedPhoenixScore)>
        {
            (titled, Score(chart, 990_000)), (untitled, Score(chart, 990_000))
        };

        var charts = new List<Chart> { chart };
        GiveFullPools(charts, scores, titled, untitled);
        var saved = new List<SavedFolder>();
        var saga = BuildSaga(charts, scores, saved,
            titleLevels: new Dictionary<int, Guid[]> { [17] = new[] { titled } });

        await saga.Consume(Context(new ProcessPumbilityTierListCommand()));

        var folder = saved.Single(f => f.Level == 20 && f.ChartType == ChartType.Single);
        Assert.Equal(2, folder.ByCohort[PumbilityCohortKeys.Community].Entries.Single().Appearances);
        var cohort = folder.ByCohort[PumbilityCohortKeys.ForDifficultyTitleLevel(17)];
        Assert.Equal(1, cohort.Entries.Single().Appearances);
        Assert.Equal(1, cohort.CohortSize);
    }

    [Fact]
    public async Task ACohortWhosePoolsReachNothingHereIsNotWritten()
    {
        var lowPlayer = Guid.NewGuid();
        var lowChart = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).Build();
        var highChart = new ChartBuilder().WithLevel(24).WithType(ChartType.Single).Build();
        var scores = new List<(Guid, RecordedPhoenixScore)> { (lowPlayer, Score(lowChart, 980_000)) };
        var charts = new List<Chart> { lowChart, highChart };
        GiveFullPools(charts, scores, lowPlayer);

        var saved = new List<SavedFolder>();
        var saga = BuildSaga(charts, scores, saved,
            titleLevels: new Dictionary<int, Guid[]> { [15] = new[] { lowPlayer } });

        await saga.Consume(Context(new ProcessPumbilityTierListCommand()));

        var cohort = PumbilityCohortKeys.ForDifficultyTitleLevel(15);
        Assert.Contains(cohort, saved.Single(f => f.Level == 15).ByCohort.Keys);
        // Nobody at that level pools a 24, so the folder is written with no cohorts at all
        // rather than with a set of zeros nobody can read anything from.
        Assert.Empty(saved.Single(f => f.Level == 24).ByCohort);
    }

    [Fact]
    public async Task APartialPoolIsNotCountedAtAll()
    {
        // The Phoenix 2 shape: a player who has imported a handful of charts has a low pool
        // total because they have played little of the mix, not because they are weak. Counting
        // them would put them in a cohort of genuinely weaker players and drag that cohort with
        // them, which is what a mix with no score volume yet does to everyone in it.
        var full = Guid.NewGuid();
        var partial = Guid.NewGuid();
        var chart = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var scores = new List<(Guid, RecordedPhoenixScore)>
        {
            (full, Score(chart, 990_000)), (partial, Score(chart, 990_000))
        };
        var charts = new List<Chart> { chart };
        GiveFullPools(charts, scores, full);

        var saved = new List<SavedFolder>();
        var saga = BuildSaga(charts, scores, saved);

        await saga.Consume(Context(new ProcessPumbilityTierListCommand()));

        var community = saved.Single(f => f.Level == 20).ByCohort[PumbilityCohortKeys.Community];
        Assert.Equal(1, community.Entries.Single().Appearances);
        Assert.Equal(1, community.CohortSize);
    }

    [Fact]
    public async Task OnlyTheTopFiftyChartsCount()
    {
        var user = Guid.NewGuid();
        // Sixty charts in one folder, descending in score: the bottom ten fall out of the pool.
        var charts = Enumerable.Range(0, 60)
            .Select(_ => new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build()).ToArray();
        var scores = charts.Select((c, i) => (user, Score(c, 999_000 - i * 1000))).ToList();

        var saved = new List<SavedFolder>();
        var saga = BuildSaga(charts, scores, saved);

        await saga.Consume(Context(new ProcessPumbilityTierListCommand()));

        var community = saved.Single(f => f.Level == 20).ByCohort[PumbilityCohortKeys.Community].Entries;
        Assert.Equal(50, community.Count(e => e.Appearances == 1));
        Assert.Equal(10, community.Count(e => e.Appearances == 0));
        Assert.All(community.Where(e => e.Appearances == 0),
            e => Assert.Equal(TierListCategory.Unrecorded, e.Category));
    }

    /// <summary>
    ///     Fifty low-level charts per player, so their pool is a pool. The census ignores anyone
    ///     short of a full fifty — a partial pool's total says how much someone has imported, not
    ///     how well they play — and every fixture here is otherwise far too small to qualify.
    ///     Level 12 keeps the ballast below whatever folder is under test, so it fills the pool
    ///     without displacing the charts the test is about.
    /// </summary>
    private static void GiveFullPools(List<Chart> charts, List<(Guid, RecordedPhoenixScore)> scores,
        params Guid[] users)
    {
        var ballast = Enumerable.Range(0, 50)
            .Select(_ => new ChartBuilder().WithLevel(12).WithType(ChartType.Single).Build()).ToArray();
        charts.AddRange(ballast);
        foreach (var user in users)
        foreach (var chart in ballast)
            scores.Add((user, Score(chart, 900_000)));
    }

    private static RecordedPhoenixScore Score(Chart chart, int score)
    {
        return new RecordedPhoenixScore(chart.Id, score, PhoenixPlate.MarvelousGame, false, Recorded);
    }

    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private sealed record SavedFolder(ChartType ChartType, int Level,
        IReadOnlyDictionary<string, PumbilityTierListFolder> ByCohort);

    private static TierListSaga BuildSaga(IEnumerable<Chart> charts,
        IReadOnlyCollection<(Guid UserId, RecordedPhoenixScore Record)> scores, List<SavedFolder> saved,
        IReadOnlyDictionary<int, Guid[]>? titleLevels = null)
    {
        var all = charts.ToArray();
        var chartRepo = new Mock<IChartRepository>();
        chartRepo.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(),
                It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, DifficultyLevel? level, ChartType? type, IEnumerable<Guid>? _,
                    CancellationToken _) =>
                all.Where(c => level == null || c.Level == level).Where(c => type == null || c.Type == type));

        var byId = all.ToDictionary(c => c.Id);
        var scoreReader = new Mock<IScoreReader>();
        scoreReader.Setup(s => s.GetScores(It.IsAny<MixEnum>(), It.IsAny<ChartType>(),
                It.IsAny<DifficultyLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, ChartType type, DifficultyLevel level, CancellationToken _) =>
                scores.Where(s => byId[s.Record.ChartId].Type == type && byId[s.Record.ChartId].Level == level));

        var titles = new Mock<ITitleRepository>();
        titles.Setup(t => t.GetUserIdsOnHighestLevel(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, DifficultyLevel level, CancellationToken _) =>
                titleLevels != null && titleLevels.TryGetValue((int)level, out var users)
                    ? users
                    : Array.Empty<Guid>());

        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(c => c.SavePumbilityTierLists(It.IsAny<MixEnum>(), It.IsAny<ChartType>(),
                It.IsAny<DifficultyLevel>(),
                It.IsAny<IReadOnlyDictionary<string, PumbilityTierListFolder>>(),
                It.IsAny<CancellationToken>()))
            .Callback((MixEnum _, ChartType type, DifficultyLevel level,
                    IReadOnlyDictionary<string, PumbilityTierListFolder> byCohort,
                    CancellationToken _) =>
                saved.Add(new SavedFolder(type, level, byCohort)))
            .Returns(Task.CompletedTask);

        // The PUMBILITY rebuild reads charts, scores, titles and writes tier lists; the rest of
        // TierListSaga's dependencies belong to its other consumers and stay inert dummies here.
        return new TierListSaga(new Mock<IChartDifficultyRatingRepository>().Object, chartRepo.Object,
            tierLists.Object, scoreReader.Object, new Mock<ICurrentUserAccessor>().Object,
            new Mock<IPlayerStatsReader>().Object, new Mock<IChartScoringLevelRepository>().Object,
            new Mock<IChartScoreStatsRepository>().Object, new Mock<IFolderCohortStatsRepository>().Object,
            titles.Object);
    }
}
