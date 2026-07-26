using Bunit;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class RangeSliderTests : ComponentTestBase
{
    private IRenderedComponent<RangeSlider> Render(int min = 10, int max = 26, int valueMin = 16,
        int valueMax = 18, bool disabled = false, Action<int>? onMin = null, Action<int>? onMax = null)
    {
        return RenderComponent<RangeSlider>(p => p
            .Add(x => x.Label, "Singles")
            .Add(x => x.Prefix, "S")
            .Add(x => x.Min, min)
            .Add(x => x.Max, max)
            .Add(x => x.ValueMin, valueMin)
            .Add(x => x.ValueMax, valueMax)
            .Add(x => x.Disabled, disabled)
            .Add(x => x.ValueMinChanged, onMin ?? (_ => { }))
            .Add(x => x.ValueMaxChanged, onMax ?? (_ => { })));
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
    public void ReleasingTheMinThumbRaisesValueMinChanged()
    {
        int? changed = null;
        var cut = Render(onMin: v => changed = v);

        cut.FindAll("input[type=range]")[0].Change("14");

        Assert.Equal(14, changed);
    }

    [Fact]
    public void TheDragItselfMovesTheReadoutButPublishesNothing()
    {
        // A drag across a wide scale crosses hundreds of steps. Publishing each one re-runs
        // whatever the caller does on change — for the SRP, the whole search — which fights
        // the thumb. The readout still follows so the drag stays direct.
        var published = 0;
        var cut = Render(onMin: _ => published++);

        cut.FindAll("input[type=range]")[0].Input("14");

        Assert.Equal(0, published);
        Assert.Equal("S14 – S18", cut.Find(".range-slider-value").TextContent);

        cut.FindAll("input[type=range]")[0].Change("14");
        Assert.Equal(1, published);
    }

    [Fact]
    public void MinThumbDraggedPastMaxClampsToMax()
    {
        int? minChanged = null;
        var cut = Render(onMin: v => minChanged = v);

        cut.FindAll("input[type=range]")[0].Change("22");

        Assert.Equal(18, minChanged);
    }

    [Fact]
    public void MaxThumbDraggedBelowMinClampsToMin()
    {
        int? maxChanged = null;
        var cut = Render(onMax: v => maxChanged = v);

        cut.FindAll("input[type=range]")[1].Change("12");

        Assert.Equal(16, maxChanged);
    }
}
