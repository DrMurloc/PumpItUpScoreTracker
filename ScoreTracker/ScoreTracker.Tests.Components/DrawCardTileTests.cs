using Bunit;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Randomizer.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Enums;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class DrawCardTileTests : ComponentTestBase
{
    // The tile's DifficultyBubble gates its tooltip on RendererInfo; render it interactive.
    // Last after the base's registrations — reading the renderer locks the service collection.
    public DrawCardTileTests() => this.RenderInteractive();

    private static Chart TestChart()
    {
        var song = new Song("Nxde", SongType.Arcade, new Uri("https://example.invalid/nxde.png"),
            TimeSpan.FromMinutes(2), "Artist", null);
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix, song, ChartType.Single, DifficultyLevel.From(17),
            MixEnum.Phoenix, null, null, new HashSet<Skill>());
    }

    private IRenderedComponent<DrawCardTile> Render(DrawCardState state = DrawCardState.None,
        UiDensity density = UiDensity.Compact, bool disabled = false, Action<Guid>? onTap = null)
    {
        return RenderComponent<DrawCardTile>(p => p
            .Add(x => x.PullId, Guid.NewGuid())
            .Add(x => x.Chart, TestChart())
            .Add(x => x.Order, 4)
            .Add(x => x.State, state)
            .Add(x => x.Density, density)
            .Add(x => x.Disabled, disabled)
            .Add(x => x.OnTap, onTap ?? (_ => { })));
    }

    [Fact]
    public void OrderBadgeShowsTheDrawNumber()
    {
        var cut = Render();

        Assert.Equal("4", cut.Find(".draw-card-order").TextContent);
    }

    [Fact]
    public void ProtectedCardWearsTheHeldRingAndChip()
    {
        var cut = Render(DrawCardState.Protected);

        Assert.NotNull(cut.Find(".draw-card-held"));
        Assert.Equal("Held", cut.Find(".draw-card-state-held-chip").TextContent);
    }

    [Fact]
    public void VetoedCardDimsInPlaceWithTheVetoChip()
    {
        var cut = Render(DrawCardState.Vetoed);

        Assert.NotNull(cut.Find(".draw-card-vetoed"));
        Assert.Equal("Vetoed", cut.Find(".draw-card-state-vetoed-chip").TextContent);
        // The tile still renders — vetoed cards stay put until Clear Vetoed.
        Assert.Contains("Nxde", cut.Find(".draw-card-name").TextContent);
    }

    [Fact]
    public void CompactTapInvokesTheArmedActionCallbackWithThePullId()
    {
        Guid? tapped = null;
        var cut = Render(onTap: id => tapped = id);

        cut.Find(".draw-card").Click();

        Assert.Equal(cut.Instance.PullId, tapped);
    }

    [Fact]
    public void DisabledCompactTileIgnoresTaps()
    {
        var tapped = false;
        var cut = Render(disabled: true, onTap: _ => tapped = true);

        cut.Find(".draw-card").Click();

        Assert.False(tapped);
    }

    [Fact]
    public void DisabledComfortableTileHidesTheActionRow()
    {
        var cut = Render(density: UiDensity.Comfortable, disabled: true);

        Assert.Empty(cut.FindAll(".tier-chart-card-actions"));
    }

    [Fact]
    public void ComfortableShowsProtectAndVetoUntilAStateIsSet()
    {
        var neutral = Render(density: UiDensity.Comfortable);
        Assert.NotEmpty(neutral.FindAll("button[aria-label=Protect]"));
        Assert.NotEmpty(neutral.FindAll("button[aria-label=Veto]"));
        Assert.Empty(neutral.FindAll("button[aria-label=Undo]"));

        var held = Render(DrawCardState.Protected, UiDensity.Comfortable);
        Assert.Empty(held.FindAll("button[aria-label=Protect]"));
        Assert.NotEmpty(held.FindAll("button[aria-label=Undo]"));
    }
}
