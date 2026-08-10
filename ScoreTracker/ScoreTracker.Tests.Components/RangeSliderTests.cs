using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class RangeSliderTests : ComponentTestBase
{
    private IRenderedComponent<RangeSlider> Render(int min = 10, int max = 26, int valueMin = 16,
        int valueMax = 18, bool disabled = false, Action<int>? onMin = null, Action<int>? onMax = null,
        Func<int, string>? format = null)
    {
        return RenderComponent<RangeSlider>(p => p
            .Add(x => x.Label, "Singles")
            .Add(x => x.Prefix, "S")
            .Add(x => x.Min, min)
            .Add(x => x.Max, max)
            .Add(x => x.ValueMin, valueMin)
            .Add(x => x.ValueMax, valueMax)
            .Add(x => x.Disabled, disabled)
            .Add(x => x.Format, format)
            .Add(x => x.ValueMinChanged, onMin ?? (_ => { }))
            .Add(x => x.ValueMaxChanged, onMax ?? (_ => { })));
    }

    private static IElement Thumb(IRenderedComponent<RangeSlider> cut, int index)
    {
        return cut.FindAll("input[type=range]")[index];
    }

    [Fact]
    public void PrintsTheWorkingRangeBesideTheLabel()
    {
        var cut = Render();

        Assert.Equal("S16 – S18", cut.Find(".range-slider-value").TextContent);
    }

    [Fact]
    public void DisabledReadsOff()
    {
        var cut = Render(disabled: true);

        Assert.Equal("Off", cut.Find(".range-slider-value").TextContent);
    }

    [Fact]
    public async Task ReleasingTheMinThumbRaisesValueMinChanged()
    {
        int? changed = null;
        var cut = Render(onMin: v => changed = v);

        await Thumb(cut, 0).ChangeAsync(new ChangeEventArgs { Value = "14" });

        Assert.Equal(14, changed);
    }

    [Fact]
    public void TheDragNeverReachesTheServer()
    {
        // The thumb's position must not ride the circuit. Answering each input event means
        // writing a value one latency old back onto the input, which drags the thumb backwards
        // under the finger for as long as the gesture lasts — the whole reason js/range-slider.js
        // exists. The server hears the release and nothing before it; the script paints the rest
        // off these hooks.
        var cut = Render();

        Assert.All(cut.FindAll("input[type=range]"),
            input => Assert.False(input.HasAttribute("blazor:oninput")));
        Assert.True(cut.Find(".range-slider").HasAttribute("data-range-slider"));
        Assert.True(cut.Find(".range-slider-value").HasAttribute("data-range-readout"));
        Assert.True(cut.Find(".range-slider-fill").HasAttribute("data-range-fill"));
    }

    [Fact]
    public void TheThumbsSitWhereTheCommittedRangeIs()
    {
        var cut = Render();

        Assert.Equal("16", Thumb(cut, 0).GetAttribute("value"));
        Assert.Equal("18", Thumb(cut, 1).GetAttribute("value"));
    }

    [Fact]
    public async Task MinThumbDraggedPastMaxSwapsTheEnds()
    {
        // Clamping mid-drag was the same yank the circuit round-trip caused, so the thumbs
        // cross and the pair sorts itself on release.
        int? minChanged = null;
        int? maxChanged = null;
        var cut = Render(onMin: v => minChanged = v, onMax: v => maxChanged = v);

        await Thumb(cut, 0).ChangeAsync(new ChangeEventArgs { Value = "22" });

        Assert.Equal(18, minChanged);
        Assert.Equal(22, maxChanged);
    }

    [Fact]
    public async Task MaxThumbDraggedBelowMinSwapsTheEnds()
    {
        int? minChanged = null;
        int? maxChanged = null;
        var cut = Render(onMin: v => minChanged = v, onMax: v => maxChanged = v);

        await Thumb(cut, 1).ChangeAsync(new ChangeEventArgs { Value = "12" });

        Assert.Equal(12, minChanged);
        Assert.Equal(16, maxChanged);
    }

    [Fact]
    public async Task CoincidentThumbsAtTheTopCanStillBeSeparated()
    {
        // Both ends parked on the ceiling used to be a dead end: the max input is second in the
        // DOM so it wins every overlap, and clamping it against a coincident min left it nowhere
        // to go — Clear all was the only way out.
        int? minChanged = null;
        var cut = Render(valueMin: 26, valueMax: 26, onMin: v => minChanged = v);

        await Thumb(cut, 1).ChangeAsync(new ChangeEventArgs { Value = "20" });

        Assert.Equal(20, minChanged);
    }

    [Fact]
    public async Task AReleaseOutsideTheScaleLandsOnTheScale()
    {
        int? changed = null;
        var cut = Render(onMin: v => changed = v);

        await Thumb(cut, 0).ChangeAsync(new ChangeEventArgs { Value = "3" });

        Assert.Equal(10, changed);
    }

    [Fact]
    public void TheFillMeasuresAgainstTheThumbInsetRatherThanTheBox()
    {
        // A thumb's centre never reaches the ends of its box, so a fill drawn in plain
        // percentages ends up to half a thumb away from the thumb it is supposed to meet — and
        // the gap changes as you drag. 16 and 18 of 10..26 are 0.375 and 0.5 along the travel.
        var style = Render().Find(".range-slider-fill").GetAttribute("style") ?? string.Empty;

        Assert.Contains("0.375 * (100% - var(--range-thumb))", style);
        Assert.Contains("0.5 * (100% - var(--range-thumb))", style);
    }

    [Fact]
    public void AScaleThatFormatsHandsDownItsWordingForEveryStop()
    {
        // The script paints the readout during a drag and must not re-implement a format, so
        // C# ships the labels it would have rendered.
        var cut = Render(min: 0, max: 3, format: v => $"{v}:00");

        Assert.Equal("[\"0:00\",\"1:00\",\"2:00\",\"3:00\"]",
            cut.Find(".range-slider").GetAttribute("data-range-labels"));
    }

    [Fact]
    public void APlainScaleShipsItsPrefixInsteadOfATable()
    {
        var cut = Render();

        Assert.False(cut.Find(".range-slider").HasAttribute("data-range-labels"));
        Assert.Equal("S", cut.Find(".range-slider").GetAttribute("data-range-prefix"));
    }
}
