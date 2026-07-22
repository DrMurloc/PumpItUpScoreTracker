using Bunit;
using ScoreTracker.Web.Components;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

public sealed class FolderGridTests : ComponentTestBase
{
    [Fact]
    public void RendersTypeTabsAndTheLevelGridForTheInitialType()
    {
        var cut = RenderComponent<FolderGrid>(p => p.Add(x => x.InitialType, ChartType.Single));

        Assert.Contains("Singles", cut.Markup);
        Assert.Contains("Doubles", cut.Markup);
        Assert.Contains("CoOp", cut.Markup);
        // Singles/Doubles show the full level range.
        Assert.True(cut.FindAll(".folder-picker-level").Count > 20);
    }

    [Fact]
    public void SinglesTabStopsAt26_NoHarderSingleChartExistsYet()
    {
        var cut = RenderComponent<FolderGrid>(p => p.Add(x => x.InitialType, ChartType.Single));

        var levels = cut.FindAll(".folder-picker-level").Select(b => b.TextContent).ToArray();
        Assert.Equal("26", levels.Last());
        Assert.DoesNotContain("27", levels);
    }

    [Fact]
    public void DoublesTabGoesHigherThanSingles()
    {
        var cut = RenderComponent<FolderGrid>(p => p.Add(x => x.InitialType, ChartType.Double));

        var levels = cut.FindAll(".folder-picker-level").Select(b => int.Parse(b.TextContent)).ToArray();
        Assert.True(levels.Max() > 26, "Doubles folders should still offer levels above 26.");
    }

    [Fact]
    public void CoOpTabShowsPlayerCountsOnly()
    {
        var cut = RenderComponent<FolderGrid>(p => p.Add(x => x.InitialType, ChartType.CoOp));

        var levels = cut.FindAll(".folder-picker-level").Select(b => b.TextContent).ToArray();
        Assert.Equal(new[] { "2", "3", "4", "5" }, levels);
    }

    [Fact]
    public void SelectedCellsHighlightThroughTheCallback()
    {
        var cut = RenderComponent<FolderGrid>(p => p
            .Add(x => x.InitialType, ChartType.Single)
            .Add(x => x.IsSelected, (t, l) => t == ChartType.Single && l is >= 15 and <= 18));

        Assert.Equal(4, cut.FindAll(".folder-picker-current").Count);
    }

    [Fact]
    public void TappingALevelEmitsTheTabbedTypeAndLevel()
    {
        (ChartType Type, int Level)? picked = null;
        var cut = RenderComponent<FolderGrid>(p => p
            .Add(x => x.InitialType, ChartType.Single)
            .Add(x => x.LevelPicked, f => picked = f));

        // Switch to the Doubles tab, then pick 20 — the emit carries the tab's type.
        cut.FindAll(".folder-picker-types button")[1].Click();
        cut.FindAll(".folder-picker-level").First(b => b.TextContent == "20").Click();

        Assert.Equal((ChartType.Double, 20), picked);
    }
}
