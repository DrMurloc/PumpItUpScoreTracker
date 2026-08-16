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

        // Two runs of type-specific sections (ruler + table, then the comparison), each once per type.
        var blocks = page.FindAll("[data-pc-type]");
        Assert.Equal(new[] { "Single", "Double", "Single", "Double" }, blocks.Select(b => b.GetAttribute("data-pc-type")));
        Assert.All(blocks.Where(b => b.GetAttribute("data-pc-type") == "Single"), b => Assert.False(b.HasAttribute("hidden")));
        Assert.All(blocks.Where(b => b.GetAttribute("data-pc-type") == "Double"),
            b => Assert.True(b.HasAttribute("hidden"), "the Doubles blocks are in the HTML for a crawler and hidden until the toggle"));
        Assert.Equal(2, page.FindAll("[data-pc-type-button]").Count);
        Assert.Equal("true", page.Find("[data-pc-type-button='Single']").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void EveryValueCellIsExactlyWhatTheConfigurationSays()
    {
        var page = RenderPhoenix2();
        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);

        foreach (var block in page.FindAll("[data-pc-type]").Where(b => b.QuerySelector("table.pc-vt") != null))
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
        var singles = page.Find("[data-pc-type='Single'] table.pc-vt").QuerySelectorAll("tbody tr").ToArray();
        var doubles = page.Find("[data-pc-type='Double'] table.pc-vt").QuerySelectorAll("tbody tr").ToArray();
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
        var doubles = page.FindAll("[data-pc-type='Double']").First(b => b.QuerySelector("table.pc-vt") != null);
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
        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, b => Assert.False(b.HasAttribute("hidden")));
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

    [Fact]
    public void TheRulerAnchorsEveryRowOnItsOwnLevelAndReadsLevelsBought()
    {
        var page = RenderPhoenix2();
        var doubles = page.FindAll("[data-pc-type='Double']").First(b => b.QuerySelector(".pc-ruler") != null);
        var labels = doubles.QuerySelectorAll(".pc-ruler-lbl").Select(l => l.TextContent.Trim()).ToArray();
        Assert.Equal("D29", labels[0]);
        Assert.Equal("D10", labels[^1]);
        // The end label is the kernel's number: an SSS+ on D20 buys 4.6 levels over a 900,000.
        var d20 = Array.IndexOf(labels, "D20");
        Assert.Equal("+4.6", doubles.QuerySelectorAll(".pc-ruler-end")[d20].TextContent.Trim());
        var d24 = Array.IndexOf(labels, "D24");
        Assert.Equal("+2.8", doubles.QuerySelectorAll(".pc-ruler-end")[d24].TextContent.Trim());
        // Every row has one anchor tick and its bar segments; the tail is drawn faded.
        var tracks = doubles.QuerySelectorAll(".pc-ruler-track").ToArray();
        Assert.All(tracks, t => Assert.Single(t.QuerySelectorAll(".pc-ruler-anchor")));
        Assert.All(tracks, t => Assert.NotEmpty(t.QuerySelectorAll(".pc-seg-tail")));
        Assert.All(tracks, t => Assert.NotEmpty(t.QuerySelectorAll(".pc-seg-ice")));
    }

    [Fact]
    public void TheRulerLegendNamesBandsByTheGradesTheyCover()
    {
        var phoenix2 = RenderPhoenix2().FindAll("[data-pc-type='Single'] .pc-legend").First().TextContent;
        Assert.Contains("A+ · AA · AA+", phoenix2);
        Assert.Contains("B · A", phoenix2);
        Assert.Contains("900,000 on its own level — A+", phoenix2);

        var phoenix = RenderPhoenix().Find(".pc-legend").TextContent;
        Assert.Contains("AA · AA+", phoenix);
        Assert.DoesNotContain("A+ · AA · AA+", phoenix);
        Assert.Contains("A · A+", phoenix);
        Assert.Contains("900,000 on its own level — AA", phoenix);
    }

    [Fact]
    public void TheComparisonStatesTheRatioNotJustTheGradeSpan()
    {
        var page = RenderPhoenix2();
        var compare = page.FindAll("[data-pc-type='Double']").First(b => b.QuerySelector(".pc-cmp") != null);
        var facts = compare.QuerySelectorAll(".pc-cmp-fact").Select(f => f.TextContent.Replace('\n', ' ')).ToArray();
        Assert.Equal(3, facts.Length);
        // ① the grade span: +50% then +11.1%; ② one level: +17% then +2.2%; ③ the exchange rate.
        Assert.Contains("+50", facts[0]);
        Assert.Contains("+11.1", facts[0]);
        Assert.Contains("+17", facts[1]);
        Assert.Contains("+2.2", facts[1]);
        Assert.Contains("2.7 levels", facts[2]);
        Assert.Contains("4.6 levels", facts[2]);
        Assert.Contains("1.7×", facts[2]);
        // The paragraph names the inversion: S (970,000) there, AA+ (940,000) here for doubles.
        var answer = compare.QuerySelector(".pc-answer")!.TextContent;
        Assert.Contains("S (970,000)", answer);
        Assert.Contains("AA+ (940,000)", answer);
        // Paired bars for six levels, the other mix's bar and this mix's on every row.
        Assert.Equal(6, compare.QuerySelectorAll(".pc-cmp-bar-other").Length);
        Assert.Equal(6, compare.QuerySelectorAll(".pc-cmp-bar-mine").Length);
    }

    [Fact]
    public void ThePhoenixPageComparesAgainstPhoenix2Singles()
    {
        var page = RenderPhoenix();
        var compare = page.Find(".pc-cmp").ParentElement!.ParentElement!;
        Assert.Contains("Phoenix 2", page.Find(".pc-h2.pc-q").TextContent);
        var answer = page.Find(".pc-answer").TextContent;
        Assert.Contains("S (970,000)", answer);
        Assert.Contains("1.6×", answer);
    }

    [Fact]
    public void TheScriptsConstantsAreExactlyTheConfiguration()
    {
        // The calculator multiplies what this block says, so it must be the same numbers the
        // markup was built from — per type, with the singles-priced-up rule and the type's own
        // grade and plate tables.
        var page = RenderPhoenix2();
        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        var blocks = page.FindAll("script[data-pc-constants]");
        Assert.Equal(2, blocks.Count);
        foreach (var block in blocks)
        {
            using var json = System.Text.Json.JsonDocument.Parse(block.TextContent);
            var root = json.RootElement;
            var type = Enum.Parse<ChartType>(root.GetProperty("type").GetString()!);
            Assert.True(root.GetProperty("additive").GetBoolean());
            Assert.Equal(type == ChartType.Single, root.GetProperty("singlesUp").GetBoolean());
            Assert.Equal(type.GetShortHand(), root.GetProperty("prefix").GetString());
            Assert.Equal("A+", root.GetProperty("anchorGrade").GetString());
            foreach (var grade in Enum.GetValues<PhoenixLetterGrade>())
                Assert.Equal(scoring.LetterGradeModifierFor(grade, type), root.GetProperty("grades").GetProperty(grade.GetName()).GetDouble(), 9);
            foreach (var plate in Enum.GetValues<PhoenixPlate>())
                Assert.Equal(scoring.PlateModifierFor(plate, type), root.GetProperty("plates").GetProperty(plate.GetShorthand()).GetDouble(), 9);
            Assert.Equal(ScoringConfiguration.Phoenix2PricedBase(type, 20), root.GetProperty("levels").GetProperty("20").GetInt32());
            Assert.Equal(900_000, root.GetProperty("floors").GetProperty("A+").GetInt32());
        }
    }

    [Fact]
    public void TheCalculatorRendersItsDefaultWorkedOutBeforeAnyScriptRuns()
    {
        var page = RenderPhoenix2();
        var singles = page.FindAll("[data-pc-type='Single']").First(b => b.QuerySelector("[data-pc-calc]") != null);
        var calc = singles.QuerySelector("[data-pc-calc]")!;
        // Default: S24 · S · Marvelous Game = Base(25) 260 × (1.45 + 0.006) = 378.56.
        Assert.Equal("378.56", calc.QuerySelector("[data-pc-out]")!.TextContent.Trim());
        Assert.Equal("24", calc.QuerySelector("[data-pc-level] option[selected]")!.GetAttribute("value"));
        Assert.Equal("S", calc.QuerySelector("[data-pc-grade] option[selected]")!.GetAttribute("value"));
        Assert.Equal("MG", calc.QuerySelector("[data-pc-plate] option[selected]")!.GetAttribute("value"));
        Assert.Contains("260", calc.QuerySelector("[data-pc-math]")!.TextContent);
        Assert.Contains("Base(25)", calc.QuerySelector("[data-pc-math]")!.TextContent);

        var phoenix = RenderPhoenix().Find("[data-pc-calc]");
        Assert.Null(phoenix.QuerySelector("[data-pc-plate]"));
        // Phoenix default: level 24 · S = 1,150 × 1.20 = 1,380.00.
        Assert.Equal("1,380.00", phoenix.QuerySelector("[data-pc-out]")!.TextContent.Trim());
    }
}
