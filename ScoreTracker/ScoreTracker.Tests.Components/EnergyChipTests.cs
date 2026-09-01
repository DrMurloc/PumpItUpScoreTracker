using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Energy chip (docs/design/pumbility-overhaul.md D51): the chosen option is the label, a
///     pick raises the energy for the frame to remember, and no option is spelled with a judgement's
///     key.
/// </summary>
public sealed class EnergyChipTests : ComponentTestBase
{
    [Fact]
    public void TheChipPrintsTheChosenEnergyAsItsOwnLabel()
    {
        var cut = RenderComponent<EnergyChip>(p => p.Add(x => x.Value, Energy.Great));

        Assert.Contains("Great energy", cut.Find("[data-testid=energy-chip-label]").TextContent);
    }

    [Fact]
    public async Task PickingAnOptionRaisesItAndPickingTheCurrentOneRaisesNothing()
    {
        // The menu's items render through MudBlazor's popover provider rather than inside the
        // chip's own tree, so the provider is rendered beside the chip to give them somewhere to land.
        Energy? picked = null;
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<EnergyChip>(1);
            builder.AddComponentParameter(2, nameof(EnergyChip.Value), Energy.Good);
            builder.AddComponentParameter(3, nameof(EnergyChip.ValueChanged),
                EventCallback.Factory.Create<Energy>(this, e => picked = e));
            builder.CloseComponent();
        });

        await cut.Find("[data-testid=energy-chip-label]").ClickAsync(new MouseEventArgs());
        await Option(cut, "Top of my game").ClickAsync(new MouseEventArgs());
        Assert.Equal(Energy.TopOfMyGame, picked);

        picked = null;
        await cut.Find("[data-testid=energy-chip-label]").ClickAsync(new MouseEventArgs());
        await Option(cut, "Good energy").ClickAsync(new MouseEventArgs());
        Assert.Null(picked);
    }

    /// <summary>
    ///     An open menu's item, found by the label a reader clicks rather than a Mud class — and
    ///     outside the chip itself, whose own label is one of the same phrases.
    /// </summary>
    private static AngleSharp.Dom.IElement Option(IRenderedFragment cut, string label)
    {
        cut.WaitForAssertion(() => Assert.Contains(label, cut.Markup));
        return Assert.Single(cut.FindAll("*").Where(e =>
            e.Children.Length == 0 && e.TextContent.Trim() == label && e.Closest("[data-testid=energy-chip-label]") == null));
    }

    [Fact]
    public void NoOptionIsSpelledWithAJudgementsKey()
    {
        // "Good" and "Great" are the judgement names' resx keys; an option reusing one would print
        // the judgement's translation in Korean and Japanese, and a case variant is forbidden.
        var labels = EnergyChip.Options.Select(EnergyChip.Label).ToArray();

        Assert.Equal(new[] { "Good energy", "Great energy", "Top of my game" }, labels);
        Assert.DoesNotContain("Good", labels);
        Assert.DoesNotContain("Great", labels);
    }
}
