using Bunit;
using Microsoft.AspNetCore.Components.Web;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class FolderGridTests : ComponentTestBase
{
    [Fact]
    public void RendersFolderTabsAndTheLevelGridForTheInitialFolder()
    {
        var cut = RenderComponent<FolderGrid>(p => p.Add(x => x.InitialCategory, ChartTypeCategory.Single));

        Assert.Contains("Singles", cut.Markup);
        Assert.Contains("Doubles", cut.Markup);
        Assert.Contains("CoOp", cut.Markup);
        // Singles/Doubles show the full level range.
        Assert.True(cut.FindAll(".folder-picker-level").Count > 20);
    }

    [Fact]
    public void SinglesTabStopsAt26_NoHarderSingleChartExistsYet()
    {
        var cut = RenderComponent<FolderGrid>(p => p.Add(x => x.InitialCategory, ChartTypeCategory.Single));

        var levels = cut.FindAll(".folder-picker-level").Select(b => b.TextContent).ToArray();
        Assert.Equal("26", levels.Last());
        Assert.DoesNotContain("27", levels);
    }

    [Fact]
    public void DoublesTabGoesHigherThanSingles()
    {
        var cut = RenderComponent<FolderGrid>(p => p.Add(x => x.InitialCategory, ChartTypeCategory.Double));

        var levels = cut.FindAll(".folder-picker-level").Select(b => int.Parse(b.TextContent)).ToArray();
        Assert.True(levels.Max() > 26, "Doubles folders should still offer levels above 26.");
    }

    [Fact]
    public void CoOpTabShowsPlayerCountsOnly()
    {
        var cut = RenderComponent<FolderGrid>(p => p.Add(x => x.InitialCategory, ChartTypeCategory.CoOp));

        var levels = cut.FindAll(".folder-picker-level").Select(b => b.TextContent).ToArray();
        Assert.Equal(new[] { "2", "3", "4", "5" }, levels);
    }

    // RISE is singles and half-doubles: two tabs, the second named for the one chart type its
    // doubles folder holds, and no CoOp tab at all (docs/design/rise.md §3.1).
    [Fact]
    public void RiseOffersHalfDoublesInsteadOfDoublesAndHasNoCoOpTab()
    {
        var cut = RenderComponent<FolderGrid>(p => p
            .Add(x => x.InitialCategory, ChartTypeCategory.Single)
            .Add(x => x.Mix, MixEnum.Rise));

        var tabs = cut.FindAll(".folder-picker-types button").Select(b => b.TextContent).ToArray();
        Assert.Equal(2, tabs.Length);
        Assert.Contains("Singles", tabs);
        Assert.Contains("H. DOUBLE", tabs);
        Assert.DoesNotContain(tabs, t => t.Contains("CoOp"));
    }

    [Fact]
    public void AMixWithOrdinaryDoublesStillSaysDoubles()
    {
        var cut = RenderComponent<FolderGrid>(p => p
            .Add(x => x.InitialCategory, ChartTypeCategory.Single)
            .Add(x => x.Mix, MixEnum.RiseArcade));

        var tabs = cut.FindAll(".folder-picker-types button").Select(b => b.TextContent).ToArray();
        Assert.Contains("Doubles", tabs);
        Assert.DoesNotContain(tabs, t => t.Contains("H. DOUBLE"));
    }

    // A folder the mix does not have cannot be the opening tab, however the host asked.
    [Fact]
    public void AFolderTheMixDoesNotHaveIsNeverTheOpeningTab()
    {
        var cut = RenderComponent<FolderGrid>(p => p
            .Add(x => x.InitialCategory, ChartTypeCategory.CoOp)
            .Add(x => x.Mix, MixEnum.Rise));

        var levels = cut.FindAll(".folder-picker-level").Select(b => b.TextContent).ToArray();
        Assert.NotEqual(new[] { "2", "3", "4", "5" }, levels);
    }

    [Fact]
    public void SelectedCellsHighlightThroughTheCallback()
    {
        var cut = RenderComponent<FolderGrid>(p => p
            .Add(x => x.InitialCategory, ChartTypeCategory.Single)
            .Add(x => x.IsSelected, (c, l) => c == ChartTypeCategory.Single && l is >= 15 and <= 18));

        Assert.Equal(4, cut.FindAll(".folder-picker-current").Count);
    }

    [Fact]
    public async Task TappingALevelEmitsTheTabbedFolderAndLevel()
    {
        (ChartTypeCategory Category, int Level)? picked = null;
        var cut = RenderComponent<FolderGrid>(p => p
            .Add(x => x.InitialCategory, ChartTypeCategory.Single)
            .Add(x => x.LevelPicked, f => picked = f));

        // Switch to the Doubles tab, then pick 20 — the emit carries the tab's folder.
        await cut.FindAll(".folder-picker-types button")[1].ClickAsync(new MouseEventArgs());
        await cut.FindAll(".folder-picker-level").First(b => b.TextContent == "20")
            .ClickAsync(new MouseEventArgs());

        Assert.Equal((ChartTypeCategory.Double, 20), picked);
    }

    [Fact]
    public void EveryFolderIsPickableUnlessAHostSaysOtherwise()
    {
        var cut = RenderComponent<FolderGrid>(p => p.Add(x => x.InitialCategory, ChartTypeCategory.Double));

        Assert.Empty(cut.FindAll(".folder-picker-level-off"));
        Assert.DoesNotContain(cut.FindAll(".folder-picker-level"), b => b.HasAttribute("disabled"));
    }

    [Fact]
    public void AFolderTheHostHasNothingForRendersInertButStaysVisible()
    {
        // Dimmed, not hidden: the holes are the map of where the change did not land.
        var cut = RenderComponent<FolderGrid>(p => p
            .Add(x => x.InitialCategory, ChartTypeCategory.Double)
            .Add(x => x.IsMissing, (c, l) => !(c == ChartTypeCategory.Double && l == 21)));

        var cells = cut.FindAll(".folder-picker-level");
        var enabled = cells.Where(b => !b.HasAttribute("disabled")).ToArray();
        Assert.Equal("21", Assert.Single(enabled).TextContent);
        Assert.True(cells.Count > 20, "Disabled folders still render — they are information.");
    }

    [Fact]
    public void AFolderWithNoEnabledLevelHasItsTabDisabled()
    {
        var cut = RenderComponent<FolderGrid>(p => p
            .Add(x => x.InitialCategory, ChartTypeCategory.Double)
            .Add(x => x.IsMissing, (c, _) => c != ChartTypeCategory.Double));

        var tabs = cut.FindAll(".folder-picker-types button");
        Assert.True(tabs.First(b => b.TextContent.Contains("Singles")).HasAttribute("disabled"));
        Assert.False(tabs.First(b => b.TextContent.Contains("Doubles")).HasAttribute("disabled"));
    }

    [Fact]
    public void TheGridOpensOnAFolderThatHasSomethingRatherThanTheRequestedDeadTab()
    {
        var cut = RenderComponent<FolderGrid>(p => p
            .Add(x => x.InitialCategory, ChartTypeCategory.Single)
            .Add(x => x.IsMissing, (c, _) => c != ChartTypeCategory.Double));

        // Doubles goes above 26; landing there proves the dead Singles tab was skipped.
        var levels = cut.FindAll(".folder-picker-level").Select(b => int.Parse(b.TextContent)).ToArray();
        Assert.True(levels.Max() > 26);
    }
}
