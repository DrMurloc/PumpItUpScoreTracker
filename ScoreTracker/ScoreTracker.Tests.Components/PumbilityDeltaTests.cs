using Bunit;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The compact gain rule: as much precision as the size of the gain needs and no more,
///     truncated at every rung so a badge never reads higher than the pool actually moved.
/// </summary>
public sealed class PumbilityDeltaTests : ComponentTestBase
{
    [Theory]
    // Ten and over is whole, with a separator once it needs one.
    [InlineData(137.06, "137")]
    [InlineData(10.99, "10")]
    [InlineData(1255.87, "1,255")]
    // Between one and ten, a single decimal.
    [InlineData(9.99, "9.9")]
    [InlineData(1.0, "1.0")]
    [InlineData(4.567, "4.5")]
    // Under one, two decimals — the rung that could not exist while gains were ints.
    [InlineData(0.42, "0.42")]
    [InlineData(0.999, "0.99")]
    [InlineData(0.004, "0.00")]
    public void AGainShowsOnlyThePrecisionItsSizeNeeds(double value, string expected)
    {
        Assert.Equal(expected, PumbilityFormat.Gain(value));
    }

    [Fact]
    public void FloatNoiseDoesNotCostTheLastDigit()
    {
        // A 0.4 gain arrives from a chain of double arithmetic looking like this. Truncating
        // it as-is prints 0.39 — a wrong number produced by the rule meant to prevent wrong
        // numbers — so the noise comes off first.
        Assert.Equal("0.40", PumbilityFormat.Gain(0.3999999999999773));
        Assert.Equal("430", PumbilityFormat.Gain(429.99999999999994));
    }

    [Theory]
    // Cleaning the noise cannot be the whole answer, because these values ARE clean and the
    // error lands after: the nearest double to 0.29 times 100 is 28.999999999999996, so a
    // truncation done in doubles printed "0.28". The scaling happens in decimal for exactly
    // these three, which are every value under one that the old arithmetic got wrong.
    [InlineData(0.29, "0.29")]
    [InlineData(0.57, "0.57")]
    [InlineData(0.58, "0.58")]
    public void ScalingToARungDoesNotReintroduceTheNoiseItJustRemoved(double value, string expected)
    {
        Assert.Equal(expected, PumbilityFormat.Gain(value));
    }

    [Fact]
    public void AnOfficialDecimalIsNotRoundTrippedThroughADouble()
    {
        // The board quotes two places already. Nothing has to be cleaned off one, and going
        // via double to find that out is the one way to damage it.
        Assert.Equal("0.29", PumbilityFormat.Gain(0.29m));
        Assert.Equal("1,255", PumbilityFormat.Gain(1255.87m));
    }

    [Fact]
    public void TruncationIsAppliedToTheMagnitudeSoALossIsNotOverstated()
    {
        // Math.Floor(-0.5) is -1. Flooring the signed value would report a bigger drop than
        // happened, which is the same lie as an inflated gain wearing a minus sign.
        Assert.Equal(PumbilityFormat.Gain(0.5), PumbilityFormat.Gain(-0.5));
        Assert.Equal("137", PumbilityFormat.Gain(-137.06));
    }

    [Fact]
    public void TheComponentSignsTheNumberAndCarriesItsClass()
    {
        var cut = RenderComponent<PumbilityDelta>(p => p
            .Add(x => x.Value, 137.06)
            .Add(x => x.Class, "sbd-delta"));

        Assert.Equal("+137", cut.Find("span").TextContent);
        Assert.Contains("sbd-delta", cut.Find("span").ClassList);
    }

    [Fact]
    public void TheMarkerIsTheCallersChoice()
    {
        // Boards point an arrow at a gain where badges sign it, and both spellings predate
        // this component — neither surface had to change its vocabulary to adopt it.
        var cut = RenderComponent<PumbilityDelta>(p => p
            .Add(x => x.Value, 9.99)
            .Add(x => x.Marker, "▲"));

        Assert.Equal("▲9.9", cut.Find("span").TextContent);
    }
}
