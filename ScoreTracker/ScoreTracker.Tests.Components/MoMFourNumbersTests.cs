using System;
using System.Linq;
using Bunit;
using Microsoft.AspNetCore.Components;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.Web.Components.MoM;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     "Where the points came from" (docs/design/march-of-murlocs.md §11.6): the four numbers with
///     their places on the board, the folder figure beside the balanced one, and one mark per
///     session on each strip — yours lit, the compare target in the accent.
/// </summary>
public sealed class MoMFourNumbersTests : ComponentTestBase
{
    public MoMFourNumbersTests() => SetRendererInfo(new RendererInfo("Server", true));

    private IRenderedComponent<MoMFourNumbers> Render(MoMSessionView view, Guid? highlight = null)
    {
        return RenderComponent<MoMFourNumbers>(p => p
            .Add(f => f.Levers, view.Levers)
            .Add(f => f.Places, view.Places)
            .Add(f => f.Board, view.BoardSessions)
            .Add(f => f.SessionId, view.SessionId)
            .Add(f => f.HighlightSessionId, highlight));
    }

    [Fact]
    public void FourCellsWithTheirPlacesAndTheFolderFigureBesideTheBalancedOne()
    {
        var view = MoMComponentData.Session();

        var cut = Render(view);

        var cells = cut.FindAll(".mom-four > div");
        Assert.Equal(4, cells.Count);
        Assert.Contains("3", cells[0].QuerySelector(".v")!.TextContent);
        Assert.Contains("2nd of 3", cells[0].QuerySelector(".rank")!.TextContent);
        Assert.Contains("24.66", cells[1].QuerySelector(".v")!.TextContent);
        Assert.Contains("24.00 by folder", cells[1].QuerySelector(".sub")!.TextContent);
        Assert.Contains("Highest on the board", cells[1].QuerySelector(".rank")!.TextContent);
        Assert.Contains("932,719", cells[2].QuerySelector(".v")!.TextContent);
        Assert.Single(cells[2].QuerySelectorAll(".v img"));
        Assert.Contains("Least on the board", cells[3].QuerySelector(".rank")!.TextContent);
        Assert.DoesNotContain("lever", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EveryStripCarriesOneMarkPerSessionWithYouAndTheTargetLit()
    {
        var view = MoMComponentData.Session();

        var cut = Render(view, MoMComponentData.RivalSessionId);

        foreach (var strip in cut.FindAll(".dots"))
        {
            Assert.Equal(3, strip.QuerySelectorAll("i").Length);
            Assert.Single(strip.QuerySelectorAll("i.you"));
            Assert.Single(strip.QuerySelectorAll("i.them"));
        }

        // Worst on the left, best on the right: the most charts sits last on the charts strip.
        var charts = cut.FindAll(".mom-four > div")[0].QuerySelectorAll(".dots i").ToArray();
        Assert.Contains("you", charts[1].ClassName ?? string.Empty); // 3 charts sits between 2 and 4
    }

    [Fact]
    public void ADraftOffTheBoardSaysSoInsteadOfRanking()
    {
        var view = MoMComponentData.Session(draft: true) with { SessionId = Guid.NewGuid() };

        var cut = Render(view);

        Assert.All(cut.FindAll(".rank"), r => Assert.Contains("not on the board", r.TextContent));
        Assert.Empty(cut.FindAll(".dots i.you"));
    }
}
