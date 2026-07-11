using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class ChartTests
{
    [Fact]
    public void CoOpWithoutOverrideDerivesPlayerCountFromLevel()
    {
        var chart = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(3).Build();

        Assert.Equal(3, chart.PlayerCount);
    }

    [Fact]
    public void NonCoOpWithoutOverrideHasOnePlayer()
    {
        var chart = new ChartBuilder().WithType(ChartType.Double).WithLevel(21).Build();

        Assert.Equal(1, chart.PlayerCount);
    }

    [Fact]
    public void OverrideWinsSoLegacyCoOpsCanCarryARealDifficulty()
    {
        // An Infinity Routine-era co-op: D15-equivalent difficulty, 2 players.
        var chart = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(15).WithPlayerCount(2).Build();

        Assert.Equal(2, chart.PlayerCount);
        Assert.Equal(15, (int)chart.Level);
    }

    [Fact]
    public void SlotDefaultsToNullForModernCharts()
    {
        var chart = new ChartBuilder().Build();

        Assert.Null(chart.Slot);
    }

    [Fact]
    public void HalfDoubleShortHandIsHdb()
    {
        var chart = new ChartBuilder().WithType(ChartType.HalfDouble).WithLevel(12).Build();

        Assert.Equal("HDB12", chart.DifficultyString);
    }

    [Fact]
    public void DifficultyDisplayIsSlotAwareBecauseSlotsAreIdentity()
    {
        // The Perfect Collection's Final Audition 2 has BOTH a Hard 6 and a Crazy 6 —
        // "S6" alone cannot name either of them.
        var hard = new ChartBuilder().WithType(ChartType.Single).WithLevel(6).WithSlot(LegacySlot.Hard).Build();
        var crazy = new ChartBuilder().WithType(ChartType.Single).WithLevel(6).WithSlot(LegacySlot.Crazy).Build();
        var modern = new ChartBuilder().WithType(ChartType.Single).WithLevel(6).Build();

        Assert.Equal("Hard 6", hard.DifficultyDisplay);
        Assert.Equal("Crazy 6", crazy.DifficultyDisplay);
        Assert.Equal("S6", modern.DifficultyDisplay);
    }

    [Theory]
    [InlineData("Crazy", LegacySlot.Crazy)]
    [InlineData("Another Crazy", LegacySlot.AnotherCrazy)]
    [InlineData("Another Nightmare", LegacySlot.AnotherNightmare)]
    [InlineData("FREESTYLE", LegacySlot.Freestyle)]
    public void LegacySlotParsesStoredForms(string stored, LegacySlot expected)
    {
        Assert.True(LegacySlotHelperMethods.TryParseLegacySlot(stored, out var slot));
        Assert.Equal(expected, slot);
    }

    [Fact]
    public void LegacySlotNamesRestoreTheSpacedDisplayForm()
    {
        Assert.Equal("Another Crazy", LegacySlot.AnotherCrazy.GetName());
        Assert.Equal("Crazy", LegacySlot.Crazy.GetName());
    }
}
