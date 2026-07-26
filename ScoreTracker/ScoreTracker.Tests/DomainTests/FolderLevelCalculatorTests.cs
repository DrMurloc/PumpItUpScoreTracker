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
