using Bunit;
using Microsoft.AspNetCore.Components.Web;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The judgement strip (D43): a play's counts in the game's own colour vocabulary. A
///     strip with somewhere to go is a button; a bare readout is a span; null counts never
///     reach the component at all.
/// </summary>
public sealed class JudgementStripTests : ComponentTestBase
{
    [Fact]
    public void RendersEveryCountAndTheMaxCombo()
    {
        var cut = RenderComponent<JudgementStrip>(p => p
            .Add(s => s.Judgements, new JudgementCounts(731, 74, 11, 4, 18, 214)));

        Assert.Equal("731", cut.Find(".judg-perfect").TextContent);
        Assert.Equal("74", cut.Find(".judg-great").TextContent);
        Assert.Equal("11", cut.Find(".judg-good").TextContent);
        Assert.Equal("4", cut.Find(".judg-bad").TextContent);
        Assert.Equal("18", cut.Find(".judg-miss").TextContent);
        Assert.Equal("×214", cut.Find(".judg-strip-combo").TextContent);
    }

    [Fact]
    public void AnUnsolvedComboSaysNothingRatherThanZero()
    {
        // MaxCombo is solved from the breakdown and can be unknowable (no note count, a
        // breakdown that falls short) — ×0 would claim a fact nobody measured.
        var cut = RenderComponent<JudgementStrip>(p => p
            .Add(s => s.Judgements, new JudgementCounts(731, 74, 11, 4, 18)));

        Assert.Empty(cut.FindAll(".judg-strip-combo"));
    }

    [Fact]
    public async Task WithSomewhereToGoTheStripIsAButtonAndOpens()
    {
        var opened = false;
        var cut = RenderComponent<JudgementStrip>(p => p
            .Add(s => s.Judgements, new JudgementCounts(700, 1, 2, 3, 4, 500))
            .Add(s => s.OnOpen, () => opened = true));

        await cut.Find("button.judg-strip").ClickAsync(new MouseEventArgs());

        Assert.True(opened);
    }

    [Fact]
    public void AsAPlainReadoutThereIsNoButton()
    {
        var cut = RenderComponent<JudgementStrip>(p => p
            .Add(s => s.Judgements, new JudgementCounts(700, 1, 2, 3, 4, 500)));

        Assert.Empty(cut.FindAll("button"));
        Assert.NotNull(cut.Find("span.judg-strip"));
    }
}
