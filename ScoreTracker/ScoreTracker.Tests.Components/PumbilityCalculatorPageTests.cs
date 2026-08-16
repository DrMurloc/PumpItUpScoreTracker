using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.Tools;
using ScoreTracker.Web.Services.Contracts;
using Xunit;
using Chart = ScoreTracker.SharedKernel.Models.Chart;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The PUMBILITY formula page as static markup (docs/design/pumbility-calculator.md). The
///     arithmetic is pinned in the kernel's tests; these pin that the page prints exactly what
///     the configuration says — every value cell equals GetScore for its level, type and grade
///     floor — and that each mix gets its own shape.
/// </summary>
public sealed class PumbilityCalculatorPageTests : ComponentTestBase
{
    private readonly Mock<IUiSettingsAccessor> _settings = new();

    public PumbilityCalculatorPageTests()
    {
        _settings.Setup(s => s.GetSelectedMix()).ReturnsAsync(MixEnum.Phoenix2);
        Services.AddSingleton(_settings.Object);
    }

    private static Chart Make(ChartType type, int level, MixEnum mix)
    {
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix,
            new Song("Song", SongType.Arcade, new Uri("https://piu.test/art.png"), TimeSpan.FromMinutes(2), "Doin",
                Bpm.From(180, 180)),
            type, level, mix, null, 1000, new HashSet<Skill>());
    }

    private void Catalog(MixEnum mix, params Chart[] charts)
    {
        Mediator.Setup(m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == mix), It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
    }

    private IRenderedComponent<PumbilityCalculator> RenderPhoenix2()
    {
        Catalog(MixEnum.Phoenix2,
            Make(ChartType.Single, 20, MixEnum.Phoenix2), Make(ChartType.Single, 20, MixEnum.Phoenix2),
            Make(ChartType.Single, 26, MixEnum.Phoenix2),
            Make(ChartType.Double, 24, MixEnum.Phoenix2), Make(ChartType.Double, 29, MixEnum.Phoenix2),
            Make(ChartType.CoOp, 3, MixEnum.Phoenix2));
        return RenderComponent<PumbilityCalculator>(p => p.Add(x => x.MixSlug, "phoenix-2"));
    }

    private IRenderedComponent<PumbilityCalculator> RenderPhoenix()
    {
        Catalog(MixEnum.Phoenix,
            Make(ChartType.Single, 20, MixEnum.Phoenix), Make(ChartType.Double, 20, MixEnum.Phoenix),
            Make(ChartType.Single, 28, MixEnum.Phoenix), Make(ChartType.CoOp, 3, MixEnum.Phoenix),
            Make(ChartType.CoOp, 4, MixEnum.Phoenix));
        return RenderComponent<PumbilityCalculator>(p => p.Add(x => x.MixSlug, "phoenix"));
    }

    [Fact]
    public void Phoenix2RendersBothTypesWithTheSecondHidden()
    {
        var page = RenderPhoenix2();

        var blocks = page.FindAll("[data-pc-type]");
        Assert.Equal(new[] { "Single", "Double" }, blocks.Select(b => b.GetAttribute("data-pc-type")));
        Assert.False(blocks[0].HasAttribute("hidden"));
        Assert.True(blocks[1].HasAttribute("hidden"), "the Doubles block is in the HTML for a crawler and hidden until the toggle");
        Assert.Equal(2, page.FindAll("[data-pc-type-button]").Count);
        Assert.Equal("true", page.Find("[data-pc-type-button='Single']").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void EveryValueCellIsExactlyWhatTheConfigurationSays()
    {
        var page = RenderPhoenix2();
        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);

        foreach (var block in page.FindAll("[data-pc-type]"))
        {
            var type = Enum.Parse<ChartType>(block.GetAttribute("data-pc-type")!);
            var cells = block.QuerySelectorAll("td.pc-v[data-v]").ToArray();
            Assert.NotEmpty(cells);
            foreach (var cell in cells)
            {
                var level = int.Parse(cell.GetAttribute("data-l")!);
                var grade = PhoenixLetterGradeHelperMethods.TryParse(cell.GetAttribute("data-g"))!.Value;
                var expected = scoring.GetScore(type, level, grade.GetMinimumScoreFor(MixEnum.Phoenix2),
                    PhoenixPlate.RoughGame);
                var printed = double.Parse(cell.GetAttribute("data-v")!, CultureInfo.InvariantCulture);
                Assert.Equal(expected, printed, 9);
                Assert.Equal(Math.Round(expected).ToString("N0"), cell.TextContent.Trim());
            }
        }
    }

    [Fact]
    public void Phoenix2RowsRunFromTheCatalogsTopLevelDownToTen()
    {
        var page = RenderPhoenix2();
        var singles = page.Find("[data-pc-type='Single']").QuerySelectorAll("tbody tr").ToArray();
        var doubles = page.Find("[data-pc-type='Double']").QuerySelectorAll("tbody tr").ToArray();
        // Singles stop at 26 (the highest single in the catalog), doubles at 29.
        Assert.Equal(26 - 10 + 1, singles.Length);
        Assert.Equal(29 - 10 + 1, doubles.Length);
        Assert.StartsWith("S26", singles[0].QuerySelector("td.pc-lv")!.TextContent.Trim());
        Assert.StartsWith("D29", doubles[0].QuerySelector("td.pc-lv")!.TextContent.Trim());
        // A Single is priced a level up: S20's row shows Base(21) = 235; D24 shows 250.
        var s20 = singles.Single(r => r.QuerySelector("td.pc-lv")!.TextContent.StartsWith("S20"));
        Assert.Contains("235", s20.QuerySelector("td.pc-lv small")!.TextContent);
        var d24 = doubles.Single(r => r.QuerySelector("td.pc-lv")!.TextContent.StartsWith("D24"));
        Assert.Contains("250", d24.QuerySelector("td.pc-lv small")!.TextContent);
        // Chart counts ride the last column: two S20s in this catalog.
        Assert.Equal("2", s20.QuerySelector("td.pc-n")!.TextContent.Trim());
    }

    [Fact]
    public void Phoenix2FootnotesTheExtrapolatedLevelsAndHasNoCoOpRow()
    {
        var page = RenderPhoenix2();
        var doubles = page.Find("[data-pc-type='Double']");
        Assert.NotNull(doubles.QuerySelector("tbody tr td.pc-lv sup"));
        Assert.Contains("28 / 29", doubles.QuerySelector(".pc-table-foot")!.TextContent);
        Assert.Contains("290, 300", doubles.QuerySelector(".pc-table-foot")!.TextContent);
        Assert.Empty(page.FindAll("tr.pc-coop"));
        // The Phoenix 2 floors sit in the headers.
        var headers = doubles.QuerySelectorAll("thead th").Select(h => h.TextContent).ToArray();
        Assert.Contains(headers, h => h.Contains("AA") && h.Contains("920k"));
        Assert.Contains(headers, h => h.Contains("A+") && h.Contains("900k"));
    }

    [Fact]
    public void PhoenixRendersOneTableWithTheCoOpRowAndNoTypeToggle()
    {
        var page = RenderPhoenix();

        var blocks = page.FindAll("[data-pc-type]");
        Assert.Single(blocks);
        Assert.False(blocks[0].HasAttribute("hidden"));
        Assert.Empty(page.FindAll("[data-pc-type-button]"));

        var coop = page.Find("tr.pc-coop");
        Assert.Contains("2,000", coop.QuerySelector("td.pc-lv")!.TextContent);
        // 2,000 × 1.50 for the SSS+ column, and two CO-OP charts in the catalog.
        Assert.Equal("3,000", coop.QuerySelectorAll("td.pc-v").Last().TextContent.Trim());
        Assert.Equal("2", coop.QuerySelector("td.pc-n")!.TextContent.Trim());
        Assert.Empty(page.FindAll("td.pc-lv sup"));

        // One table for both types: the level-20 row counts the single AND the double.
        var row20 = page.Find("table.pc-vt").QuerySelectorAll("tbody tr")
            .Single(r => r.QuerySelector("td.pc-lv")!.TextContent.Trim().StartsWith("20"));
        Assert.Equal("2", row20.QuerySelector("td.pc-n")!.TextContent.Trim());
        // And every cell is Phoenix's own arithmetic.
        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, false);
        var s = row20.QuerySelectorAll("td.pc-v[data-v]").Single(c => c.GetAttribute("data-g") == "S");
        Assert.Equal(scoring.GetScore(ChartType.Single, 20, PhoenixLetterGrade.S.GetMinimumScoreFor(MixEnum.Phoenix), PhoenixPlate.RoughGame),
            double.Parse(s.GetAttribute("data-v")!, CultureInfo.InvariantCulture), 9);
    }

    [Fact]
    public void TheConstantsTablesSpellOutOnlyTheCellsWhereSinglesDiffer()
    {
        var page = RenderPhoenix2();
        var tables = page.FindAll("table.pc-ct");
        Assert.Equal(2, tables.Count);
        var gradeRows = tables[0].QuerySelectorAll("tbody tr").ToArray();
        // Double, Single, score floor.
        Assert.Equal(3, gradeRows.Length);
        Assert.Equal(7, gradeRows[1].QuerySelectorAll("td.pc-diff").Length);
        Assert.Equal(16 - 7, gradeRows[1].QuerySelectorAll("td.pc-same").Length);
        var plateRows = tables[1].QuerySelectorAll("tbody tr").ToArray();
        Assert.Equal(2, plateRows.Length);
        Assert.Equal(2, plateRows[1].QuerySelectorAll("td.pc-diff").Length);
        Assert.Contains("+0.020", plateRows[0].TextContent);
    }

    [Fact]
    public void PhoenixConstantsSayEveryPlateIsOne()
    {
        var page = RenderPhoenix();
        var tables = page.FindAll("table.pc-ct");
        var plateRows = tables[1].QuerySelectorAll("tbody tr").ToArray();
        Assert.Single(plateRows);
        Assert.All(plateRows[0].QuerySelectorAll("td.pc-num"), td => Assert.Equal("×1.0", td.TextContent.Trim()));
    }

    [Fact]
    public void TheWorkedExamplesAreRealArithmetic()
    {
        var page = RenderPhoenix2();
        var examples = page.FindAll(".pc-example").Select(e => e.TextContent.Replace("\n", " ")).ToArray();
        Assert.Equal(4, examples.Length);
        // D24 · S · Marvelous Game = 250 × (1.45 + 0.006) = 364.00
        Assert.Contains(examples, e => e.Contains("D24") && e.Contains("250") && e.Contains("1.45") && e.Contains("0.006") && e.Contains("364.00"));
        // S17 pays Base(18) = 220 — the singles-one-level-up rule made visible.
        Assert.Contains(examples, e => e.Contains("S17") && e.Contains("220"));
    }

    [Fact]
    public void TheBareRouteServesTheViewersMix()
    {
        Catalog(MixEnum.Phoenix2, Make(ChartType.Single, 20, MixEnum.Phoenix2));
        var page = RenderComponent<PumbilityCalculator>();
        Assert.Contains("Phoenix 2", page.Find(".pc-eyebrow").TextContent);
        Assert.Contains("/PumbilityCalculator/phoenix", page.Find(".pc-eyebrow a").GetAttribute("href"));
    }
}
