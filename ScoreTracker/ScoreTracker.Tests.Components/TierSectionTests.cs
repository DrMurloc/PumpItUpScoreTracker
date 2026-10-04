using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     A tier section folds by default; a list read as a reference passes Collapsible="false" and
///     gets the same stripe, name and count as a heading with nothing to press
///     (docs/UX-GUIDELINES.md rule 3; docs/design/official-leaderboards-overhaul.md L2).
/// </summary>
public sealed class TierSectionTests : ComponentTestBase
{
    [Fact]
    public async Task ASectionIsAButtonThatFoldsByDefault()
    {
        bool? folded = null;
        var cut = RenderComponent<TierSection>(p => p
            .Add(x => x.Name, "Highest")
            .Add(x => x.Count, 4)
            .Add(x => x.CollapsedChanged, (bool c) => folded = c)
            .AddChildContent("<span class=\"probe\">cards</span>"));

        var header = cut.Find(".tier-section-header");
        Assert.Equal("button", header.GetAttribute("role"));
        Assert.Equal("0", header.GetAttribute("tabindex"));
        Assert.NotNull(header.QuerySelector(".mud-icon-root"));
        Assert.Single(cut.FindAll(".probe"));

        await header.ClickAsync(new MouseEventArgs());

        Assert.True(folded);
    }

    [Fact]
    public void ASectionThatDoesNotFoldIsAHeadingWithItsBodyAlwaysShown()
    {
        var cut = RenderComponent<TierSection>(p => p
            .Add(x => x.Name, "Highest")
            .Add(x => x.Count, 4)
            .Add(x => x.Collapsible, false)
            // A stale fold from a caller cannot hide the body of a section that does not fold.
            .Add(x => x.Collapsed, true)
            .AddChildContent("<span class=\"probe\">cards</span>"));

        var header = cut.Find(".tier-section-header");
        Assert.Contains("tier-section-header-static", header.ClassName);
        Assert.False(header.HasAttribute("role"));
        Assert.False(header.HasAttribute("tabindex"));
        Assert.False(header.HasAttribute("aria-expanded"));
        Assert.False(header.HasAttribute("blazor:onclick"));
        Assert.Null(header.QuerySelector(".mud-icon-root"));
        Assert.Equal("Highest", cut.Find(".tier-section-name").TextContent);
        Assert.Equal("4", cut.Find(".tier-section-count").TextContent);
        Assert.Single(cut.FindAll(".probe"));
    }
}
