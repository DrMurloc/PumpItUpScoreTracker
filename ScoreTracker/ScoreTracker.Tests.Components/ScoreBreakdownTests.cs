using System.Linq;
using Bunit;
using Microsoft.AspNetCore.Components;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The score's stacked variant (UX rule 3): grade over plate with the number beside them,
///     emitted as one .sbd-score-stack grid the caller can drop into any row, with the hover
///     text on a title rather than a tooltip wrapper that would break the grid.
/// </summary>
public sealed class ScoreBreakdownTests : ComponentTestBase
{
    public ScoreBreakdownTests() => SetRendererInfo(new RendererInfo("Server", true));

    [Fact]
    public void StackedRendersTheGridWithBothIconsAndTheNumber()
    {
        var cut = RenderComponent<ScoreBreakdown>(p => p
            .Add(s => s.Score, PhoenixScore.From(984346))
            .Add(s => s.Plate, PhoenixPlate.MarvelousGame)
            .Add(s => s.Mix, MixEnum.Phoenix)
            .Add(s => s.ShowScore, true)
            .Add(s => s.Stacked, true));

        var stack = cut.Find(".sbd-score-stack.sbd-score-stack-fixed");
        var images = stack.QuerySelectorAll("img").ToArray();
        Assert.Equal(2, images.Length);
        Assert.Contains("letters/ss", images[0].GetAttribute("src"));
        Assert.Contains("plates/mg", images[1].GetAttribute("src"));
        Assert.Contains("984,346", stack.QuerySelector("p")!.TextContent);
        // The hover text is the score itself, as the tooltip path prints it (the peer standing
        // that used to ride here moved to PeerScore).
        Assert.Equal("984,346", stack.GetAttribute("title"));
        Assert.Empty(cut.FindAll(".mud-tooltip-root"));

        var iconsOnly = RenderComponent<ScoreBreakdown>(p => p
            .Add(s => s.Score, PhoenixScore.From(984346))
            .Add(s => s.Stacked, true));
        Assert.Equal("984,346", iconsOnly.Find(".sbd-score-stack").GetAttribute("title"));
    }

    [Fact]
    public void TheDefaultShapeIsUnchanged()
    {
        var cut = RenderComponent<ScoreBreakdown>(p => p
            .Add(s => s.Score, PhoenixScore.From(984346))
            .Add(s => s.Plate, PhoenixPlate.MarvelousGame)
            .Add(s => s.ShowScore, true)
            .Add(s => s.OneLine, true));

        Assert.Empty(cut.FindAll(".sbd-score-stack"));
        Assert.Equal(2, cut.FindAll("img").Count);
    }

    [Fact]
    public void ABrokenStackWearsTheBrokenLetterAndNoInferredPlate()
    {
        var cut = RenderComponent<ScoreBreakdown>(p => p
            .Add(s => s.Score, PhoenixScore.From(1_000_000))
            .Add(s => s.IsBroken, true)
            .Add(s => s.ShowScore, true)
            .Add(s => s.Stacked, true));

        var images = cut.FindAll(".sbd-score-stack img");
        Assert.Single(images);
        Assert.Contains("_broken", images[0].GetAttribute("src"));
    }
}
