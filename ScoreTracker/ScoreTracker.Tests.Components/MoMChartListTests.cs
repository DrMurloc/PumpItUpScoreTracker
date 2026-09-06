using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Web.Components.MoM;
using ScoreTracker.Web.Enums;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The session's charts in three densities (D21, D23, D31): the bubble before the jacket, the
///     stacked score, the session points in the corner, no play button anywhere, and every density
///     opening the same chart from its jacket.
/// </summary>
public sealed class MoMChartListTests : ComponentTestBase
{
    public MoMChartListTests() => SetRendererInfo(new RendererInfo("Server", true));

    private IRenderedComponent<MoMChartList> Render(UiDensity density, MoMChartList.ChartSort sort = MoMChartList.ChartSort.Order,
        System.Action<Chart>? onOpen = null)
    {
        var view = MoMComponentData.Session();
        return RenderComponent<MoMChartList>(p => p
            .Add(l => l.Charts, view.Charts)
            .Add(l => l.Density, density)
            .Add(l => l.Sort, sort)
            .Add(l => l.Mix, MixEnum.Phoenix)
            .Add(l => l.OnOpen, onOpen == null ? default : EventCallback.Factory.Create(this, onOpen)));
    }

    [Fact]
    public void ComfortableIsTheJacketCardWithTheStackedScoreAndThePointsChip()
    {
        var cut = Render(UiDensity.Comfortable);

        var cards = cut.FindAll("[data-testid=mom-chart-card]");
        Assert.Equal(3, cards.Count);
        Assert.Contains("Slam", cards[0].QuerySelector(".hl-card-song")!.TextContent);
        Assert.NotEmpty(cards[0].QuerySelectorAll(".sbd-score-stack-fixed"));
        Assert.Contains("1,528 pts", cards[0].QuerySelector(".hl-card-gain")!.TextContent);
        Assert.Contains("at 0:00", cards[0].TextContent);
        Assert.Contains("closing chart", cards[2].TextContent);
        Assert.Contains("▲ 24.0", cards[1].TextContent); // Adrenaline Blaster carries a balance bump
        Assert.Empty(cut.FindAll(".tier-chart-card-play"));
        Assert.DoesNotContain("AutoPlay", cut.Markup);
    }

    [Fact]
    public void CompactIsTheStickerWithTheGradeAndThePointsAndASecondLineForAnUnprintedSort()
    {
        var plain = Render(UiDensity.Compact);
        Assert.Equal(3, plain.FindAll("[data-testid=mom-chart-sticker]").Count);
        Assert.Empty(plain.FindAll(".mom-sticker-sub"));
        Assert.Contains("1,528", plain.FindAll(".mom-sticker-pts")[0].TextContent);

        var byPace = Render(UiDensity.Compact, MoMChartList.ChartSort.PointsPerSecond);
        Assert.Equal(3, byPace.FindAll(".mom-sticker-sub").Count);
        Assert.Contains("/s", byPace.FindAll(".mom-sticker-sub")[0].TextContent);
    }

    [Fact]
    public void TableKeepsThePlayOrderNumberWhateverTheSort()
    {
        var cut = Render(UiDensity.Table, MoMChartList.ChartSort.Points);

        var rows = cut.FindAll("[data-testid=mom-chart-row]");
        Assert.Equal(3, rows.Count);
        Assert.Contains("Gargoyle", rows[0].TextContent); // most points first
        Assert.Equal("3", rows[0].QuerySelector("td")!.TextContent); // but it was played third
        Assert.Contains("sorted", cut.FindAll("th").First(th => th.TextContent.Contains("Points")).ClassList);
        // Bubble before jacket in the chart cell.
        var images = rows[0].QuerySelectorAll("img").Select(i => i.GetAttribute("src") ?? string.Empty).ToList();
        Assert.True(images.FindIndex(s => s.Contains("difficulty")) < images.FindIndex(s => s.Contains("Gargoyle")));
    }

    [Fact]
    public async Task EveryDensityOpensTheChartFromItsJacket()
    {
        foreach (var (density, selector) in new[]
                 {
                     (UiDensity.Comfortable, "[data-testid=mom-chart-card] .hl-card-art"),
                     (UiDensity.Compact, "[data-testid=mom-chart-sticker]"),
                     (UiDensity.Table, "[data-testid=mom-chart-row]")
                 })
        {
            Chart? opened = null;
            var cut = Render(density, onOpen: c => opened = c);
            await cut.Find(selector).ClickAsync(new MouseEventArgs());
            Assert.NotNull(opened);
            Assert.Equal("Slam", opened!.Song.Name.ToString());
        }
    }
}
