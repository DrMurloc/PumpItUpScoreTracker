using System;
using System.Linq;
using System.Text.Json;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class SeasonIdTests
{
    [Fact]
    public void AllTimeIsZeroAndSaysSo()
    {
        Assert.Equal((short)0, SeasonId.AllTime.Value);
        Assert.True(SeasonId.AllTime.IsAllTime);
        Assert.Equal("0", SeasonId.AllTime.ToString());
    }

    [Fact]
    public void AQuarterSpellsItselfAsYearThenQuarter()
    {
        var fall = SeasonId.From(2026, 4);

        Assert.Equal((short)20264, fall.Value);
        Assert.Equal(2026, fall.Year);
        Assert.Equal(4, fall.Quarter);
        Assert.False(fall.IsAllTime);
        Assert.Equal("20264", fall.ToString());
    }

    [Fact]
    public void TheStoredNumberRoundTripsAndZeroIsAllTime()
    {
        Assert.Equal(SeasonId.From(2026, 3), SeasonId.From((short)20263));
        Assert.Equal(SeasonId.AllTime, SeasonId.From((short)0));
        short stored = SeasonId.From(2027, 1);
        Assert.Equal((short)20271, stored);
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 5)]
    [InlineData(2025, 4)]
    [InlineData(3277, 1)]
    public void ANumberThatIsNotAQuarterIsRefused(int year, int quarter)
    {
        Assert.Throws<InvalidSeasonIdException>(() => SeasonId.From(year, quarter));
    }

    [Theory]
    [InlineData((short)20265)]
    [InlineData((short)20260)]
    [InlineData((short)7)]
    [InlineData((short)-1)]
    public void AStoredNumberThatIsNotAQuarterIsRefused(short value)
    {
        Assert.Throws<InvalidSeasonIdException>(() => SeasonId.From(value));
    }

    [Fact]
    public void AllTimeHasNoYearOrQuarter()
    {
        Assert.Throws<InvalidSeasonIdException>(() => SeasonId.AllTime.Year);
        Assert.Throws<InvalidSeasonIdException>(() => SeasonId.AllTime.Quarter);
    }

    [Fact]
    public void SeasonsOrderByTimeWithAllTimeFirst()
    {
        var shuffled = new[]
        {
            SeasonId.From(2027, 1), SeasonId.AllTime, SeasonId.From(2026, 4), SeasonId.From(2026, 3)
        };

        var ordered = shuffled.OrderBy(s => s).Select(s => s.Value).ToArray();

        Assert.Equal(new short[] { 0, 20263, 20264, 20271 }, ordered);
    }

    [Fact]
    public void JsonCarriesTheNumberAndComesBackEqual()
    {
        var fall = SeasonId.From(2026, 4);

        var json = JsonSerializer.Serialize(fall);
        var back = JsonSerializer.Deserialize<SeasonId>(json);

        Assert.Equal("20264", json);
        Assert.Equal(fall, back);
        Assert.Equal("0", JsonSerializer.Serialize(SeasonId.AllTime));
        Assert.Equal(SeasonId.AllTime, JsonSerializer.Deserialize<SeasonId>("0"));
    }

    [Fact]
    public void FormattingIgnoresTheCultureAndTheFormatString()
    {
        IFormattable fall = SeasonId.From(2026, 4);

        Assert.Equal("20264", fall.ToString("N2", new System.Globalization.CultureInfo("de-DE")));
    }

    [Fact]
    public void TryFromAnswersWithoutThrowingAndFallsBackToAllTime()
    {
        Assert.True(SeasonId.TryFrom(20264, out var fall));
        Assert.Equal(SeasonId.From(2026, 4), fall);
        Assert.True(SeasonId.TryFrom(0, out var allTime));
        Assert.True(allTime.IsAllTime);
        Assert.False(SeasonId.TryFrom(20265, out var junk));
        Assert.True(junk.IsAllTime);
        Assert.False(SeasonId.TryFrom(-3, out _));
    }
}
