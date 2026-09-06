using System;
using System.Linq;
using System.Threading;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Pages.Competition.MoM;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Rules page (docs/design/march-of-murlocs.md §11.11, D42): static HTML whose every
///     number is read off MoMRules — the grade table with the letter and plate images, the
///     worked example, the continuous-scale sentence — with the season page's links and the
///     section frame around it.
/// </summary>
public sealed class MoMRulesPageTests : ComponentTestBase
{
    private readonly Mock<IUiSettingsAccessor> _settings = new();
    private MixEnum _mix = MixEnum.Phoenix2;

    public MoMRulesPageTests()
    {
        _settings.Setup(s => s.GetSelectedMix(It.IsAny<CancellationToken>())).ReturnsAsync(() => _mix);
        Services.AddSingleton(_settings.Object);
        // 6 September 2026 sits in the third quarter: Summer is the lit tile.
        Services.AddSingleton(Mock.Of<IDateTimeOffsetAccessor>(d =>
            d.Now == new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero)));
        SetRendererInfo(new RendererInfo("Static", false));
    }

    [Fact]
    public void TheGradeTableIsReadOffTheContractWithImagesForEveryRung()
    {
        var cut = RenderComponent<Rules>();

        var table = cut.Find("[data-testid=mom-rl-grades]");
        var rows = table.QuerySelectorAll("tbody tr");
        Assert.Equal(3, rows.Length);
        // Twelve letters A … SSS+ plus the perfect-game plate, images only — no letter spelled out beside one.
        Assert.Equal(12, table.QuerySelectorAll("thead img[src*='/letters/']").Length);
        Assert.Single(table.QuerySelectorAll("thead img[src*='/plates/pg']"));
        Assert.DoesNotContain("AA+", table.QuerySelector("thead")!.TextContent);

        var plus = cut.Find("[data-testid=mom-rl-plus]").QuerySelectorAll("td").Skip(1).Select(c => c.TextContent.Trim()).ToArray();
        Assert.Equal(new[] { "0×", "0.7×", "0.8×", "0.9×", "1×", "1.1×", "1.2×", "1.26×", "1.32×", "1.38×", "1.44×", "1.5×", "1.6×" }, plus);
        var scores = rows[0].QuerySelectorAll("td").Skip(1).Select(c => c.TextContent.Trim()).ToArray();
        Assert.Equal("800,000", scores[0]);
        Assert.Equal("920,000", scores[2]);
        Assert.Equal("950,000", scores[4]);
        Assert.Equal("1,000,000", scores[^1]);
        // The comparison row is regular Phoenix PUMBILITY, straight from the engine.
        var regular = rows[1].QuerySelectorAll("td").Skip(1).Select(c => c.TextContent.Trim()).ToArray();
        Assert.Equal("0.8×", regular[0]);
        Assert.Equal("1.1×", regular[4]);
        Assert.Equal("1.5×", regular[^1]);
    }

    [Fact]
    public void TheWorkedExampleAndTheContinuousSentenceCarryTheEngineNumbers()
    {
        var cut = RenderComponent<Rules>();

        var example = cut.Find("[data-testid=mom-rl-example]").TextContent;
        Assert.Contains("940,000", example);
        Assert.Contains("1,160 × 0.9 × 2 = 2,088", example);
        Assert.Contains("= 2,320", example);
        Assert.Contains("890,000 pays 0.63×", cut.Markup);
        Assert.Contains("935,000 pays 0.88×", cut.Markup);
        Assert.Contains("+50 at 22, +300 at 24, +750 at 26, +1,800 at 29", cut.Markup);
        Assert.Contains("pays 1,450; one that scores like a 25 pays 1,800", cut.Markup);
        Assert.Contains("worth 232 points here", cut.Markup);
    }

    [Fact]
    public void TheGraphicsAreInlineSvgAndTheLiveQuarterIsLit()
    {
        var cut = RenderComponent<Rules>();

        var figures = cut.FindAll("svg.mom-rl-svg");
        Assert.Equal(3, figures.Count);
        Assert.All(figures, f => Assert.False(string.IsNullOrEmpty(f.GetAttribute("aria-label"))));
        Assert.DoesNotContain("NaN", string.Join("", figures.Select(f => f.OuterHtml)));
        // Eight letter images on the ramp's axis and the plate at the top of it.
        Assert.Equal(8, figures[2].QuerySelectorAll("image[href*='/letters/']").Length);
        Assert.Equal(2, figures[2].QuerySelectorAll("image[href*='/plates/pg']").Length);

        var quarters = cut.FindAll("[data-testid=mom-rl-quarter]");
        Assert.Equal(4, quarters.Count);
        var live = Assert.Single(quarters, q => q.ClassList.Contains("live"));
        Assert.Contains("Summer", live.TextContent);
    }

    [Fact]
    public void TheEyebrowAndTheFootReturnToTheSeasonAndTheFrameIsThere()
    {
        var cut = RenderComponent<Rules>();

        Assert.Equal(MoMText.SeasonRoute, cut.Find(".pmb-eyebrow-link").GetAttribute("href"));
        Assert.Equal(MoMText.SeasonRoute, cut.Find(".mom-rl-foot a").GetAttribute("href"));
        Assert.Contains("Phoenix 2 · March of Murlocs", cut.Find(".pmb-eyebrow").TextContent);
        Assert.NotNull(cut.Find("[data-testid=mom-past-seasons]"));
        Assert.Equal(12, cut.FindAll("[data-testid=mom-rl-qa]").Count);
        Assert.Contains("So As are worthless?", cut.Markup);
        Assert.Contains("My friends are all abusing 8 6 full song!", cut.Markup);
    }

    [Fact]
    public void ThePageNamesTheViewerMixInTheEyebrowOnly()
    {
        _mix = MixEnum.Phoenix;
        var cut = RenderComponent<Rules>();

        // The tables are the one PUMBILITY+ tuning whatever the mix (D41); only the eyebrow follows the viewer.
        Assert.Contains("Phoenix · March of Murlocs", cut.Find(".pmb-eyebrow").TextContent);
        Assert.Contains("0.7×", cut.Find("[data-testid=mom-rl-plus]").TextContent);
        Assert.DoesNotContain("PUMBILITY2+", cut.Markup);
        Assert.Equal(1.6, MoMRules.PerfectGameMultiplier(MixEnum.Phoenix2));
    }
}
