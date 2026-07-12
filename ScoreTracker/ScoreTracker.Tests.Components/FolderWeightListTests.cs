using Bunit;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

public sealed class FolderWeightListTests : ComponentTestBase
{
    private static IDictionary<int, int> Weights(params (int level, int weight)[] entries)
    {
        var weights = Enumerable.Range(1, 28).ToDictionary(l => l, _ => 0);
        foreach (var (level, weight) in entries) weights[level] = weight;
        return weights;
    }

    private IRenderedComponent<FolderWeightList> Render(IDictionary<int, int> weights,
        ChartType type = ChartType.Single, Action? onChanged = null)
    {
        return RenderComponent<FolderWeightList>(p => p
            .Add(x => x.Weights, weights)
            .Add(x => x.Type, type)
            .Add(x => x.Changed, onChanged ?? (() => { })));
    }

    [Fact]
    public void RendersARowPerActiveLevelWithItsWeight()
    {
        var cut = Render(Weights((15, 1), (17, 3)));

        var rows = cut.FindAll(".weight-row");
        Assert.Equal(2, rows.Count);
        Assert.Equal("S15", rows[0].QuerySelector(".weight-row-label")!.TextContent);
        Assert.Equal("S17", rows[1].QuerySelector(".weight-row-label")!.TextContent);
    }

    [Fact]
    public void AddPickerTogglesALevelInAtWeightOneAndStaysOpenForMultiAdd()
    {
        var changed = 0;
        var weights = Weights((15, 1));
        var cut = Render(weights, onChanged: () => changed++);

        cut.Find(".weight-add-btn").Click();
        var levelButtons = cut.FindAll(".folder-picker-level");
        levelButtons.First(b => b.TextContent == "20").Click();
        cut.FindAll(".folder-picker-level").First(b => b.TextContent == "21").Click();

        Assert.Equal(1, weights[20]);
        Assert.Equal(1, weights[21]);
        Assert.Equal(2, changed);
    }

    [Fact]
    public void RemovingARowZeroesTheLevel()
    {
        var weights = Weights((15, 2), (16, 1));
        var cut = Render(weights);

        cut.FindAll(".weight-row-remove")[0].Click();

        Assert.Equal(0, weights[15]);
        Assert.Single(cut.FindAll(".weight-row"));
    }

    [Fact]
    public void EditingAWeightWritesTheDictionary()
    {
        var weights = Weights((15, 1));
        var cut = Render(weights);

        cut.Find(".weight-row input").Change("5");

        Assert.Equal(5, weights[15]);
    }

    [Fact]
    public void CoOpLabelsUsePlayerCounts()
    {
        var weights = new Dictionary<int, int> { { 2, 1 }, { 3, 0 }, { 4, 0 }, { 5, 0 } };
        var cut = Render(weights, ChartType.CoOp);

        Assert.Contains("×2", cut.Find(".weight-row-label").TextContent);
    }
}
