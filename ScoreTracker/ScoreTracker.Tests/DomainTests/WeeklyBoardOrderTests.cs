using System;
using System.Linq;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestData;
using ScoreTracker.WeeklyChallenge.Contracts;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The canonical Phoenix 1 weekly-board order: level descending, singles before doubles
///     within a level, co-ops last with the 2-player duet last of all.
/// </summary>
public sealed class WeeklyBoardOrderTests
{
    private static string Order(params (ChartType Type, int Level)[] charts)
    {
        var built = charts
            .Select((c, i) => new ChartBuilder()
                .WithType(c.Type).WithLevel(c.Level).WithSongName($"song{i}").Build())
            .ToArray();
        // Shuffle-proof: reverse the input so a no-op sort can't accidentally pass.
        return string.Join(" ", built.Reverse()
            .OrderBy(WeeklyBoardOrder.SortKey)
            .Select(c => $"{(c.Type == ChartType.CoOp ? "C" : c.Type.GetShortHand())}{(c.Type == ChartType.CoOp ? c.PlayerCount : (int)c.Level)}"));
    }

    [Fact]
    public void HardestFirstDescendingByLevel()
    {
        Assert.Equal("D26 S24 S20", Order((ChartType.Single, 20), (ChartType.Double, 26), (ChartType.Single, 24)));
    }

    [Fact]
    public void SinglesComeBeforeDoublesWithinALevel()
    {
        // The correction: single of level N reads before the double of level N.
        Assert.Equal("S26 D26 S25 D25", Order(
            (ChartType.Double, 25), (ChartType.Single, 25),
            (ChartType.Double, 26), (ChartType.Single, 26)));
    }

    [Fact]
    public void CoOpsSortLastAndTheTwoPlayerDuetIsLastOfAll()
    {
        Assert.Equal("S15 C3 C4 C5 C2", Order(
            (ChartType.CoOp, 2), (ChartType.CoOp, 5), (ChartType.Single, 15),
            (ChartType.CoOp, 3), (ChartType.CoOp, 4)));
    }

    [Fact]
    public void SparseTopDoublesLeadThenTheFirstSharedLevel()
    {
        // D28/D27 have no single peers (singles cap at 26), so they lead; then S26 before D26.
        Assert.Equal("D28 D27 S26 D26", Order(
            (ChartType.Double, 26), (ChartType.Single, 26),
            (ChartType.Double, 27), (ChartType.Double, 28)));
    }

    [Fact]
    public void NullChartSortsToTheEnd()
    {
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ordered = new[] { null, single }.OrderBy(WeeklyBoardOrder.SortKey).ToArray();
        Assert.Equal(single, ordered[0]);
        Assert.Null(ordered[1]);
    }
}
