using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class FolderLevelCalculatorTests
{
    private static Chart Chart(ChartType type, int level, Guid id) =>
        new ChartBuilder().WithId(id).WithType(type).WithLevel(level).Build();

    /// <summary>A folder of <paramref name="size" /> charts, the first <paramref name="scores" />.Length scored.</summary>
    private static (Chart[] Charts, Dictionary<Guid, int> Scores) Folder(ChartType type, int level, int size,
        params int[] scores)
    {
        var charts = Enumerable.Range(0, size).Select(_ => Chart(type, level, Guid.NewGuid())).ToArray();
        var byChart = new Dictionary<Guid, int>();
        for (var i = 0; i < scores.Length; i++) byChart[charts[i].Id] = scores[i];
        return (charts, byChart);
    }

    [Fact]
    public void AveragesOnlyThePlayedChartsSoUnplayedNeverDragTheGradeDown()
    {
        var (charts, scores) = Folder(ChartType.Single, 22, 10, 950_000, 930_000, 910_000);

        var folder = FolderLevelCalculator.Compute(MixEnum.Phoenix, charts, scores).Single();

        Assert.Equal(10, folder.Size);
        Assert.Equal(3, folder.Played);
        Assert.Equal(930_000, folder.AverageScore);
        Assert.Equal(PhoenixLetterGrade.AAPlus, folder.Grade);
    }

    [Fact]
    public void AnUntouchedFolderHasNoGradeRatherThanAnF()
    {
        var (charts, scores) = Folder(ChartType.Double, 20, 8);

        var folder = FolderLevelCalculator.Compute(MixEnum.Phoenix, charts, scores).Single();

        Assert.Equal(0, folder.Played);
        Assert.Equal(0, folder.CompletionPercent);
        Assert.Null(folder.Grade);
        Assert.False(folder.IsLamped);
    }

    [Fact]
    public void CompletionFloorsSoAlmostCompleteNeverReadsAsALamp()
    {
        var (charts, scores) = Folder(ChartType.Single, 22, 97,
            Enumerable.Repeat(940_000, 96).ToArray());

        var folder = FolderLevelCalculator.Compute(MixEnum.Phoenix, charts, scores).Single();

        Assert.Equal(98, folder.CompletionPercent);
        Assert.False(folder.IsLamped);
        Assert.Equal(80, folder.Tier);
    }

    [Fact]
    public void EveryChartPlayedIsALampAtOneHundredPercent()
    {
        var (charts, scores) = Folder(ChartType.Single, 24, 4, 900_000, 880_000, 870_000, 890_000);

        var folder = FolderLevelCalculator.Compute(MixEnum.Phoenix, charts, scores).Single();

        Assert.True(folder.IsLamped);
        Assert.Equal(100, folder.CompletionPercent);
        Assert.Equal(FolderCompletionTier.Lamp, folder.Tier);
    }

    [Fact]
    public void GrowingTheFolderMovesCompletionButNeverTheGrade()
    {
        var (charts, scores) = Folder(ChartType.Single, 22, 50, Enumerable.Repeat(940_000, 45).ToArray());
        var before = FolderLevelCalculator.Compute(MixEnum.Phoenix, charts, scores).Single();

        // Fifty new charts land in the folder and nobody has played them.
        var grown = charts.Concat(Enumerable.Range(0, 50)
            .Select(_ => Chart(ChartType.Single, 22, Guid.NewGuid()))).ToArray();
        var after = FolderLevelCalculator.Compute(MixEnum.Phoenix, grown, scores).Single();

        Assert.Equal(90, before.CompletionPercent);
        Assert.Equal(45, after.CompletionPercent);
        Assert.Equal(before.AverageScore, after.AverageScore);
        Assert.Equal(before.Grade, after.Grade);
    }

    [Fact]
    public void FoldersAreSplitByChartTypeSoS18AndD18AreSeparate()
    {
        var singles = Folder(ChartType.Single, 18, 4, 990_000, 990_000);
        var doubles = Folder(ChartType.Double, 18, 6, 900_000);
        var scores = singles.Scores.Concat(doubles.Scores).ToDictionary(kv => kv.Key, kv => kv.Value);

        var folders = FolderLevelCalculator
            .Compute(MixEnum.Phoenix, singles.Charts.Concat(doubles.Charts), scores)
            .ToDictionary(f => f.Folder);

        Assert.Equal(2, folders.Count);
        Assert.Equal(990_000, folders["S18"].AverageScore);
        Assert.Equal(900_000, folders["D18"].AverageScore);
    }

    [Fact]
    public void CoOpFoldersKeyOnPlayerCountAndBehaveLikeAnyOtherFolder()
    {
        var (charts, scores) = Folder(ChartType.CoOp, 2, 5, 980_000, 970_000);

        var folder = FolderLevelCalculator.Compute(MixEnum.Phoenix, charts, scores).Single();

        Assert.Equal(2, (int)folder.Level);
        Assert.Equal(40, folder.CompletionPercent);
        Assert.Equal(40, folder.Tier);
        Assert.Equal(975_000, folder.AverageScore);
    }

    [Fact]
    public void TheSameAverageGradesLowerInPhoenix2WhereTheFloorsAreHigher()
    {
        var (charts, scores) = Folder(ChartType.Single, 22, 2, 930_000, 930_000);

        var phoenix = FolderLevelCalculator.Compute(MixEnum.Phoenix, charts, scores).Single();
        var phoenix2 = FolderLevelCalculator.Compute(MixEnum.Phoenix2, charts, scores).Single();

        Assert.Equal(PhoenixLetterGrade.AAPlus, phoenix.Grade);
        Assert.Equal(PhoenixLetterGrade.AA, phoenix2.Grade);
    }

    [Fact]
    public void ComputeOneReturnsNullWhenTheRosterHoldsNoSuchFolder()
    {
        var (charts, scores) = Folder(ChartType.Single, 22, 3, 900_000);

        Assert.Null(FolderLevelCalculator.ComputeOne(MixEnum.Phoenix, ChartType.Single, DifficultyLevel.From(23),
            charts, scores));
        Assert.NotNull(FolderLevelCalculator.ComputeOne(MixEnum.Phoenix, ChartType.Single, DifficultyLevel.From(22),
            charts, scores));
    }

    private static FolderLevelRecord Standing(int size, int played, int average,
        MixEnum mix = MixEnum.Phoenix) =>
        new(mix, ChartType.Single, DifficultyLevel.From(22), size, played, average);

    [Fact]
    public void AFolderSeenForTheFirstTimeAnnouncesNothing()
    {
        // The seed-silently rule: without it a first import of a few thousand scores would emit a
        // milestone per folder and the Discord card would be a wall.
        Assert.Null(FolderLevelCalculator.Diff(null, Standing(100, 90, 940_000), Guid.NewGuid(),
            DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void CrossingATierAnnouncesTheCrossingAndCarriesTheCurrentGrade()
    {
        var before = Standing(100, 59, 940_000);
        var after = Standing(100, 80, 940_000);

        var milestone = FolderLevelCalculator.Diff(before, after, null, DateTimeOffset.UnixEpoch);

        Assert.NotNull(milestone);
        Assert.Equal(MilestoneKind.FolderProgress, milestone!.Kind);
        var detail = FolderProgressDetail.TryParse(milestone.Detail);
        Assert.NotNull(detail);
        Assert.True(detail!.TierMoved);
        Assert.False(detail.GradeMoved);
        Assert.Equal("40% → 80%", detail.CompletionText);
        Assert.Equal("AA+", detail.GradeText);
    }

    [Fact]
    public void ImprovingTheGradeInsideOneTierAnnouncesOnlyTheGrade()
    {
        var before = Standing(100, 90, 930_000);
        var after = Standing(100, 90, 955_000);

        var detail = FolderProgressDetail.TryParse(
            FolderLevelCalculator.Diff(before, after, null, DateTimeOffset.UnixEpoch)!.Detail);

        Assert.False(detail!.TierMoved);
        Assert.True(detail.GradeMoved);
        // The tier, not the raw 90% — a milestone reports the ladder it sits on, and the live
        // percent belongs to the surfaces that read the projection directly.
        Assert.Equal("80%", detail.CompletionText);
        Assert.Equal("AA+ → AAA", detail.GradeText);
    }

    [Fact]
    public void AFolderGainingChartsIsNotAMilestoneEvenThoughCompletionMoved()
    {
        var before = Standing(50, 45, 940_000);
        var after = Standing(100, 45, 940_000);

        Assert.Null(FolderLevelCalculator.Diff(before, after, null, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void AWeakNewPassThatDropsTheAverageIsNotAnnounced()
    {
        var before = Standing(100, 90, 940_000);
        var after = Standing(100, 91, 921_000);

        Assert.Null(FolderLevelCalculator.Diff(before, after, null, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void ALampCarriesTheHundredPercentTier()
    {
        var before = Standing(10, 9, 900_000);
        var after = Standing(10, 10, 900_000);

        var detail = FolderProgressDetail.TryParse(
            FolderLevelCalculator.Diff(before, after, null, DateTimeOffset.UnixEpoch)!.Detail);

        Assert.True(detail!.IsLamp);
        Assert.Equal("80% → 100%", detail.CompletionText);
    }

    [Fact]
    public void DetailRoundTripsThroughItsWireShape()
    {
        var detail = new FolderProgressDetail("D23", 80, PhoenixLetterGrade.AAPlus, 60, PhoenixLetterGrade.AAA);

        var parsed = FolderProgressDetail.TryParse(detail.Format());

        Assert.Equal(detail, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("S22")]
    [InlineData("S22|notanumber|AA+||")]
    [InlineData("|80|AA+||")]
    public void AnUnreadableDetailParsesToNullRatherThanThrowing(string? detail)
    {
        Assert.Null(FolderProgressDetail.TryParse(detail));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(19, 0)]
    [InlineData(20, 20)]
    [InlineData(59, 40)]
    [InlineData(60, 60)]
    [InlineData(99, 80)]
    [InlineData(100, 100)]
    public void TierIsTheHighestThresholdReached(int completionPercent, int expectedTier)
    {
        Assert.Equal(expectedTier, FolderCompletionTier.For(completionPercent));
    }
}
