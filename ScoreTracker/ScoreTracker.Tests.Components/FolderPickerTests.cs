using Bunit;
using Microsoft.AspNetCore.Components.Web;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The picker's stepper arithmetic. The grid inside its popover is covered by
///     FolderGridTests — a MudPopover's content only reaches the DOM through a provider,
///     so the useful assertions here are the ones the buttons expose directly.
/// </summary>
public sealed class FolderPickerTests : ComponentTestBase
{
    [Fact]
    public async Task ByDefaultTheSteppersWalkOneLevelAtATime()
    {
        (ChartTypeCategory Category, int Level)? picked = null;
        var cut = RenderComponent<FolderPicker>(p => p
            .Add(x => x.Category, ChartTypeCategory.Double)
            .Add(x => x.Level, 20)
            .Add(x => x.FolderChanged, f => picked = f));

        await cut.FindAll("button.mud-icon-button")[1].ClickAsync(new MouseEventArgs());

        Assert.Equal((ChartTypeCategory.Double, 21), picked);
    }

    [Fact]
    public async Task TheSteppersSkipStraightPastFoldersTheHostHasNothingFor()
    {
        // Marching one level at a time through greyed-out folders is not navigation.
        (ChartTypeCategory Category, int Level)? picked = null;
        var cut = RenderComponent<FolderPicker>(p => p
            .Add(x => x.Category, ChartTypeCategory.Double)
            .Add(x => x.Level, 20)
            .Add(x => x.IsMissing, (_, l) => l is not (20 or 25))
            .Add(x => x.FolderChanged, f => picked = f));

        await cut.FindAll("button.mud-icon-button")[1].ClickAsync(new MouseEventArgs());

        Assert.Equal((ChartTypeCategory.Double, 25), picked);
    }

    [Fact]
    public void AStepperWithNoEnabledFolderLeftIsDisabled()
    {
        var cut = RenderComponent<FolderPicker>(p => p
            .Add(x => x.Category, ChartTypeCategory.Double)
            .Add(x => x.Level, 20)
            .Add(x => x.IsMissing, (_, l) => l != 20));

        var arrows = cut.FindAll("button.mud-icon-button");
        Assert.True(arrows[0].HasAttribute("disabled"));
        Assert.True(arrows[1].HasAttribute("disabled"));
    }

    [Fact]
    public void TheLabelNamesTheFolderInView()
    {
        var cut = RenderComponent<FolderPicker>(p => p
            .Add(x => x.Category, ChartTypeCategory.Single)
            .Add(x => x.Level, 18));

        Assert.Contains("S18", cut.Markup);
    }

    // On a mix whose doubles folder is its half-doubles, the button says so rather than D18.
    [Fact]
    public void TheLabelWearsTheMixesOwnShorthand()
    {
        var cut = RenderComponent<FolderPicker>(p => p
            .Add(x => x.Category, ChartTypeCategory.Double)
            .Add(x => x.Mix, MixEnum.Rise)
            .Add(x => x.Level, 18));

        Assert.Contains("HD18", cut.Markup);
    }
}
