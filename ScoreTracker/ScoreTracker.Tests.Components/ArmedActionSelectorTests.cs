using Bunit;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Enums;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class ArmedActionSelectorTests : ComponentTestBase
{
    [Fact]
    public void RendersProtectAndVetoOnly()
    {
        // Details left the armed selector in field-test round 7 — the comfortable and
        // table views carry it instead.
        var cut = RenderComponent<ArmedActionSelector>();

        Assert.Equal(2, cut.FindAll(".armed-btn").Count);
        Assert.DoesNotContain("Details", cut.Markup);
    }

    [Fact]
    public void ActiveVetoWearsTheVetoFill()
    {
        var cut = RenderComponent<ArmedActionSelector>(p => p.Add(x => x.Value, ArmedAction.Veto));

        Assert.NotNull(cut.Find(".armed-btn-on-veto"));
        Assert.Empty(cut.FindAll(".armed-btn-on"));
    }

    [Fact]
    public void ClickingAnActionRaisesValueChanged()
    {
        ArmedAction? selected = null;
        var cut = RenderComponent<ArmedActionSelector>(p => p
            .Add(x => x.Value, ArmedAction.Protect)
            .Add(x => x.ValueChanged, a => selected = a));

        cut.FindAll(".armed-btn")[1].Click();

        Assert.Equal(ArmedAction.Veto, selected);
    }
}
