using System;
using System.Linq;
using ScoreTracker.PlayerProgress.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class HotStreakPolicyTests
{
    [Fact]
    public void EmptyPoolMeansNoGate()
    {
        Assert.Equal(1, HotStreakPolicy.PushFloor(Array.Empty<int>()));
    }

    [Fact]
    public void SingleChartPoolFloorsAtThatChart()
    {
        Assert.Equal(22, HotStreakPolicy.PushFloor(new[] { 22 }));
    }

    [Fact]
    public void QuarterRankIsNearestRankNotInterpolated()
    {
        // Four levels → rank ceil(1) = the lowest; the floor is a member of the pool,
        // never a value between members.
        Assert.Equal(20, HotStreakPolicy.PushFloor(new[] { 22, 20, 21, 20 }));
    }

    [Fact]
    public void AQuarterOfLowOutliersDoesNotDragTheFloorBelowTheBulk()
    {
        // 12 charts at 18 among 38 at 22: rank ceil(12.5) = 13 lands past the outliers.
        var pool = Enumerable.Repeat(18, 12).Concat(Enumerable.Repeat(22, 38)).ToArray();

        Assert.Equal(22, HotStreakPolicy.PushFloor(pool));
    }

    [Fact]
    public void UnsortedInputIsSortedBeforeRanking()
    {
        Assert.Equal(18, HotStreakPolicy.PushFloor(new[] { 22, 18, 20, 19 }));
    }
}
