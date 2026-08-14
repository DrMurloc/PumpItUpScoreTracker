using System;
using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components.HomeWidgets;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     A null Mixes config means "follow the current mix", and the panel's checkboxes must show
///     what that resolves to — the owner's field report was a fresh widget drawing Phoenix 2 data
///     while the panel displayed Phoenix checked.
/// </summary>
public sealed class CompetitiveLevelConfigPanelTests : ComponentTestBase
{
    private IRenderedComponent<CompetitiveLevelConfigPanel> RenderPanel(string configJson, MixEnum? effective)
    {
        var widget = new HomePageWidgetRecord(Guid.NewGuid(), "competitive-level", null, 0, "2x2", configJson, 1);
        RenderFragment panel = b =>
        {
            b.OpenComponent<CompetitiveLevelConfigPanel>(0);
            b.AddAttribute(1, nameof(CompetitiveLevelConfigPanel.Widget), widget);
            b.CloseComponent();
        };
        return base.Render(builder =>
        {
            builder.OpenComponent<CascadingValue<MixEnum?>>(0);
            builder.AddAttribute(1, "Name", "EffectiveMix");
            builder.AddAttribute(2, "Value", effective);
            builder.AddAttribute(3, "ChildContent", panel);
            builder.CloseComponent();
        }).FindComponent<CompetitiveLevelConfigPanel>();
    }

    private static (bool Phoenix, bool Phoenix2) Checked(IRenderedComponent<CompetitiveLevelConfigPanel> cut)
    {
        var boxes = cut.FindComponents<MudCheckBox<bool>>();
        return (boxes[0].Instance.Value, boxes[1].Instance.Value);
    }

    [Fact]
    public void AFollowCurrentConfigShowsTheMixTheWidgetIsActuallyDrawing()
    {
        var cut = RenderPanel("{}", MixEnum.Phoenix2);

        var (phoenix, phoenix2) = Checked(cut);
        Assert.False(phoenix);
        Assert.True(phoenix2);
    }

    [Fact]
    public void AFollowCurrentConfigOnALegacyMixFallsToPhoenixLikeTheWidgetDoes()
    {
        var cut = RenderPanel("{}", MixEnum.XX);

        var (phoenix, phoenix2) = Checked(cut);
        Assert.True(phoenix);
        Assert.False(phoenix2);
    }

    [Fact]
    public void AnExplicitMixListStillWinsOverTheCascade()
    {
        var cut = RenderPanel("{\"mixes\":[\"Phoenix\"]}", MixEnum.Phoenix2);

        var (phoenix, phoenix2) = Checked(cut);
        Assert.True(phoenix);
        Assert.False(phoenix2);
    }
}
