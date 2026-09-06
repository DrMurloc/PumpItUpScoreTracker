using System.Collections.Generic;
using System.Linq;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Whether the mirror holds a board player's whole fifty
///     (docs/design/pumbility-overhaul.md D60). The tolerance is the plate, so the arithmetic that
///     justifies it is asserted here rather than left in a comment.
/// </summary>
public sealed class BoardPoolCheckTests
{
    private static IEnumerable<(int Level, int Score)> Fifty(int level, int score)
    {
        return Enumerable.Range(0, 50).Select(_ => (level, score));
    }

    [Fact]
    public void TheToleranceIsTheWholePlateBonusOfAPoolOfPerfectGamedTwentyFives()
    {
        // Base(25) = 130 + 5*25 + 5*(25-24) = 260 for a Double, and a Perfect Game's plate bonus
        // is 0.020 — so the plate is worth 5.20 a chart and 260 across fifty. A Single prices one
        // level up, Base(26) = 270, which is the tolerance itself.
        var pg = BoardPoolCheck.Rebuild(ChartType.Double, Fifty(25, 1_000_000));
        var noPlate = 50 * 260 * 1.50;

        Assert.Equal(260d, pg - noPlate, 6);
        Assert.Equal(BoardPoolCheck.Tolerance, 50 * 270 * 0.020, 6);
    }

    [Fact]
    public void APoolShortByLessThanThePlateBandIsBelieved()
    {
        var rebuilt = BoardPoolCheck.Rebuild(ChartType.Double, Fifty(25, 1_000_000));

        Assert.True(BoardPoolCheck.Confirms(rebuilt, rebuilt + BoardPoolCheck.Tolerance));
    }

    [Fact]
    public void APoolShortByMoreThanThePlateBandIsNot()
    {
        var rebuilt = BoardPoolCheck.Rebuild(ChartType.Double, Fifty(25, 1_000_000));

        Assert.False(BoardPoolCheck.Confirms(rebuilt, rebuilt + BoardPoolCheck.Tolerance + 0.01));
    }

    [Fact]
    public void ARebuildThatOvershootsIsBelieved()
    {
        // It can only mean the plate expectation ran rich. Charts cannot be invented.
        Assert.True(BoardPoolCheck.Confirms(19_000, 18_000));
    }

    [Fact]
    public void FewerThanFiftyChartsCannotReachAFullPool()
    {
        var six = BoardPoolCheck.Rebuild(ChartType.Double, Fifty(25, 1_000_000).Take(6));

        Assert.False(BoardPoolCheck.Confirms(six, 19_000));
    }

    [Fact]
    public void OnlyTheBestFiftyCount()
    {
        var fifty = BoardPoolCheck.Rebuild(ChartType.Single, Fifty(25, 1_000_000));
        var fiftyPlusChaff = BoardPoolCheck.Rebuild(ChartType.Single,
            Fifty(25, 1_000_000).Concat(Fifty(12, 900_000)));

        Assert.Equal(fifty, fiftyPlusChaff, 6);
    }

    [Fact]
    public void AChartWorthNothingNeverOccupiesASlot()
    {
        // Below level 10 nothing prices, so a player padded with them still has no pool.
        Assert.Equal(0d, BoardPoolCheck.Rebuild(ChartType.Single, Fifty(9, 1_000_000)), 6);
    }
}
