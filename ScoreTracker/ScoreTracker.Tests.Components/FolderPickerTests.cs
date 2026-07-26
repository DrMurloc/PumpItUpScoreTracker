using System;
using System.Linq;
using Bunit;
using ScoreTracker.Web.Components;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The picker's stepper arithmetic. The grid inside its popover is covered by
///     FolderGridTests — a MudPopover's content only reaches the DOM through a provider,
///     so the useful assertions here are the ones the buttons expose directly.
/// </summary>
public sealed class FolderPickerTests : ComponentTestBase
{
    [Fact]
    public void ByDefaultTheSteppersWalkOneLevelAtATime()
    {
        (ChartType Type, int Level)? picked = null;
        var cut = RenderComponent<FolderPicker>(p => p
            .Add(x => x.Type, ChartType.Double)
            .Add(x => x.Level, 20)
            .Add(x => x.FolderChanged, f => picked = f));

        cut.FindAll("button.mud-icon-button")[1].Click();

        Assert.Equal((ChartType.Double, 21), picked);
    }

    [Fact]
    public void TheSteppersSkipStraightPastFoldersTheHostHasNothingFor()
    {
        // Marching one level at a time through greyed-out folders is not navigation.
        (ChartType Type, int Level)? picked = null;
        var cut = RenderComponent<FolderPicker>(p => p
            .Add(x => x.Type, ChartType.Double)
            .Add(x => x.Level, 20)
            .Add(x => x.IsEnabled, (_, l) => l is 20 or 25)
            .Add(x => x.FolderChanged, f => picked = f));

        cut.FindAll("button.mud-icon-button")[1].Click();

        Assert.Equal((ChartType.Double, 25), picked);
    }

    [Fact]
    public void AStepperWithNoEnabledFolderLeftIsDisabled()
    {
        var cut = RenderComponent<FolderPicker>(p => p
            .Add(x => x.Type, ChartType.Double)
            .Add(x => x.Level, 20)
            .Add(x => x.IsEnabled, (_, l) => l == 20));

        var arrows = cut.FindAll("button.mud-icon-button");
        Assert.True(arrows[0].HasAttribute("disabled"));
        Assert.True(arrows[1].HasAttribute("disabled"));
    }

    [Fact]
    public void TheLabelNamesTheFolderInView()
    {
        var cut = RenderComponent<FolderPicker>(p => p
            .Add(x => x.Type, ChartType.Single)
            .Add(x => x.Level, 18));

        Assert.Contains("S18", cut.Markup);
    }
}
