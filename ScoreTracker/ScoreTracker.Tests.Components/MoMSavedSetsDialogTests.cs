using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ScoreTracker.Web.Components.MoM;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Planner's saved sets (docs/design/march-of-murlocs.md D46). Naming is manual because the
///     generated description collided: two different sets of the same size topping out at the same
///     level carried one name and replaced each other. A named set is worth opening by name, so the
///     list opens any of them rather than only the newest.
/// </summary>
public sealed class MoMSavedSetsDialogTests : ComponentTestBase
{
    /// <summary>
    ///     A MudDialog renders its content through MudDialogProvider, not in its own place in the
    ///     tree, so the provider has to be a sibling or the component produces nothing at all.
    /// </summary>
    private IRenderedFragment RenderDialog(IReadOnlyList<string> names, bool canSave = true,
        Action<string>? onSave = null, Action<int>? onLoad = null, Action<int>? onDelete = null)
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MoMSavedSetsDialog>(1);
            builder.AddAttribute(2, nameof(MoMSavedSetsDialog.Visible), true);
            builder.AddAttribute(3, nameof(MoMSavedSetsDialog.Names), names);
            builder.AddAttribute(4, nameof(MoMSavedSetsDialog.SuggestedName), "Doubles — 44 charts, up to D24");
            builder.AddAttribute(5, nameof(MoMSavedSetsDialog.CanSave), canSave);
            builder.AddAttribute(6, nameof(MoMSavedSetsDialog.OnSave),
                EventCallback.Factory.Create<string>(this, n => onSave?.Invoke(n)));
            builder.AddAttribute(7, nameof(MoMSavedSetsDialog.OnLoad),
                EventCallback.Factory.Create<int>(this, i => onLoad?.Invoke(i)));
            builder.AddAttribute(8, nameof(MoMSavedSetsDialog.OnDelete),
                EventCallback.Factory.Create<int>(this, i => onDelete?.Invoke(i)));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void TheNameFieldOpensOnTheGeneratedDescription()
    {
        var cut = RenderDialog(Array.Empty<string>());

        var field = cut.FindComponent<MudTextField<string>>();

        Assert.Contains("charts, up to", field.Instance.Value);
    }

    [Fact]
    public async Task SavingReportsTheNameThatWasTyped()
    {
        string? saved = null;
        var cut = RenderDialog(Array.Empty<string>(), onSave: n => saved = n);
        await cut.InvokeAsync(() => cut.FindComponent<MudTextField<string>>().Instance
            .SetText("Thursday grind"));

        await cut.Find("[data-testid=mom-plan-set-save]").ClickAsync(new());

        Assert.Equal("Thursday grind", saved);
    }

    [Fact]
    public async Task ANameAlreadyInUseSaysItWillReplaceThatSet()
    {
        var cut = RenderDialog(new[] { "Thursday grind" });
        Assert.Empty(cut.FindAll("[data-testid=mom-plan-set-replaces]"));

        await cut.InvokeAsync(() => cut.FindComponent<MudTextField<string>>().Instance
            .SetText("Thursday grind"));

        Assert.Single(cut.FindAll("[data-testid=mom-plan-set-replaces]"));
    }

    [Fact]
    public async Task EverySavedSetCanBeOpened()
    {
        var opened = new List<int>();
        var cut = RenderDialog(new[] { "Newest", "Middle", "Oldest" }, onLoad: opened.Add);

        // Not only the newest: a named set is worth reaching for by name.
        await cut.FindAll("[data-testid=mom-plan-set-load]")[2].ClickAsync(new());

        Assert.Equal(new[] { 2 }, opened);
    }

    [Fact]
    public async Task ASetCanBeDeletedFromTheList()
    {
        var removed = new List<int>();
        var cut = RenderDialog(new[] { "Newest", "Oldest" }, onDelete: removed.Add);

        await cut.FindAll("[data-testid=mom-plan-set-delete]")[1].ClickAsync(new());

        Assert.Equal(new[] { 1 }, removed);
    }

    [Fact]
    public void WithNothingTickedThereIsAListButNothingToAddToIt()
    {
        var cut = RenderDialog(new[] { "Thursday grind" }, canSave: false);

        Assert.Empty(cut.FindAll("[data-testid=mom-plan-set-save]"));
        Assert.Single(cut.FindAll("[data-testid=mom-plan-set-load]"));
    }
}
