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
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
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
    public void TheTypeToggleSitsDirectlyAboveTheFirstSectionItChanges()
    {
        // The formula reads the same for both types, so a toggle up in the hero flipped nothing
        // in view. It sits under the formula, immediately above the Singles run — the ruler is
        // the first thing on the page that changes with it.
        var page = RenderPhoenix2();
        var bar = page.Find(".pc-typebar");
        Assert.NotNull(bar.QuerySelector("[data-pc-typegroup]"));
        Assert.Null(page.Find(".pc-hero").QuerySelector("[data-pc-typegroup]"));
        Assert.Equal("formula", bar.PreviousElementSibling!.Id);
        var next = bar.NextElementSibling!;
        Assert.Equal("Single", next.GetAttribute("data-pc-type"));
        Assert.NotNull(next.QuerySelector(".pc-ruler"));
    }

    /// <summary>
    ///     The return trip to your own number (docs/design/pumbility-overhaul.md D47), beside
    ///     the eyebrow's cross-mix links. Signed in only: the PUMBILITY section sends an
    ///     anonymous visitor to the front door, so an always-on link would be a dead end for
    ///     exactly the reader who arrived here from a search result.
    /// </summary>
    [Fact]
    public void TheEyebrowLinksBackToYourOwnPumbilityOnlyWhenSignedIn()
    {
        Assert.Null(RenderPhoenix2().Find(".pc-eyebrow").QuerySelector("a.pc-mine"));

        CurrentUser.Setup(u => u.IsLoggedIn).Returns(true);

        var link = RenderPhoenix2().Find(".pc-eyebrow").QuerySelector("a.pc-mine");
        Assert.NotNull(link);
        Assert.Equal("/Pumbility", link!.GetAttribute("href"));
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
    public void Phoenix2FootnotesTheCoOpBaseItPricesButNeverCounts()
    {
        // CO-OP is worth zero PUMBILITY on Phoenix 2 — but it is priced, at the engine's flat 80,
        // and that number is the CO-OP Rating. The zero rule keeps the asterisk that says so.
        var page = RenderPhoenix2();
        var zero = page.Find(".pc-zero");
        var coop = zero.QuerySelectorAll(".pc-zero-item").Single(i => i.TextContent.StartsWith("CO-OP"));
        Assert.Equal("*", coop.QuerySelector("sup.pc-mark")!.TextContent);
        var note = page.FindAll(".pc-formula .pc-foot").Select(f => f.TextContent.Replace('\n', ' ')).ToArray();
        Assert.Contains(note, n => n.Contains("a flat base of 80") && n.Contains("priced one at 2,000")
                                                                  && n.Contains("CO-OP Rating"));

        // Phoenix says it in its own words, on the row it still shows.
        var phoenix = RenderPhoenix();
        Assert.DoesNotContain(phoenix.FindAll(".pc-zero .pc-zero-item"), i => i.TextContent.StartsWith("CO-OP"));
        Assert.Contains("flat 2,000 but never counted", phoenix.Find(".pc-term-level").TextContent);
        Assert.DoesNotContain("CO-OP Rating", phoenix.Find(".pc-formula").TextContent);
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
        Assert.Contains("at 20", facts[2]);
        Assert.Contains("2.7 levels", facts[2]);
        Assert.Contains("4.6 levels", facts[2]);
        Assert.Contains("1.7×", facts[2]);
        // The paragraph names the inversion: S (970,000) there, AA+ (940,000) here for doubles.
        var answer = compare.QuerySelector(".pc-answer")!.TextContent.Replace('\n', ' ');
        Assert.Contains("S (970,000)", answer);
        Assert.Contains("AA+ (940,000)", answer);
        // Paired bars for six levels, the other mix's bar and this mix's on every row.
        Assert.Equal(6, compare.QuerySelectorAll(".pc-cmp-bar-other").Length);
        Assert.Equal(6, compare.QuerySelectorAll(".pc-cmp-bar-mine").Length);
        // The curves cross at 23 and stay crossed: from there an SSS+ buys fewer levels on
        // Phoenix 2 than it did on Phoenix — at 24, 2.8 for doubles against 3.5. Said under
        // the bars and, with the numbers, in the paragraph.
        Assert.Contains("Below 23 a 900,000 → SSS+ buys more levels on Phoenix 2 than it did on Phoenix; from 23 up it buys fewer",
            compare.QuerySelector(".pc-cmp-note")!.TextContent);
        Assert.Contains("From 23 up it flips: Phoenix 2 prices every level above 24 at double the step, so at 24 an SSS+ buys 2.8 levels here against 3.5 on Phoenix", answer);
        Assert.DoesNotContain("The gap closes", answer);
    }

    [Fact]
    public void TheSinglesComparisonFlipsAtTheSameLevel()
    {
        // Singles are priced a level up and wobble around the kink, but the crossing is 23 there
        // too: 3.3 levels at 24 against Phoenix's 3.5.
        var page = RenderPhoenix2();
        var compare = page.FindAll("[data-pc-type='Single']").First(b => b.QuerySelector(".pc-cmp") != null);
        Assert.Contains("from 23 up it buys fewer", compare.QuerySelector(".pc-cmp-note")!.TextContent);
        Assert.Contains("so at 24 an SSS+ buys 3.3 levels here against 3.5 on Phoenix", compare.QuerySelector(".pc-answer")!.TextContent.Replace('\n', ' '));
    }

    [Fact]
    public void ThePhoenixPageComparesAgainstPhoenix2Singles()
    {
        var page = RenderPhoenix();
        var compare = page.Find(".pc-cmp").Closest("section")!;
        Assert.Contains("Phoenix 2", compare.QuerySelector(".pc-h2.pc-q")!.TextContent);
        var answer = compare.QuerySelector(".pc-answer")!.TextContent;
        Assert.Contains("S (970,000)", answer);
        Assert.Contains("1.6×", answer);
        // The same crossing, read from the Phoenix side.
        Assert.Contains("from 23 up it buys fewer", compare.QuerySelector(".pc-cmp-note")!.TextContent);
        Assert.Contains("at 20", compare.QuerySelectorAll(".pc-cmp-fact")[2].TextContent);
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

    private static PumbilityPoolBandRecord Band(string key, string? title, double floor, double? ceiling, int players,
        double avgLevel, double levelPart, double scorePart, double platePart, int sOrBetterPerPool)
    {
        var charts = players * 50;
        var grades = new Dictionary<PhoenixLetterGrade, int>
        {
            [PhoenixLetterGrade.SSSPlus] = sOrBetterPerPool * players,
            [PhoenixLetterGrade.AAA] = charts - sOrBetterPerPool * players
        };
        return new PumbilityPoolBandRecord(key, title, floor, ceiling, players, charts, avgLevel * charts, levelPart,
            scorePart, platePart, grades);
    }

    private void Population(PumbilityPoolCompositionRecord? composition)
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPoolCompositionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(composition);
    }

    [Fact]
    public void WithoutASweepThePopulationSectionSaysSoAndThePlateAnswerStaysFormulaOnly()
    {
        Population(null);
        var page = RenderPhoenix2();
        var section = page.FindAll(".pc-h2.pc-q").First(h => h.TextContent.Contains("push levels")).Closest("section")!;
        Assert.Contains("Nothing to draw for Phoenix 2 yet", section.TextContent);
        Assert.Null(section.QuerySelector(".pmb-wpc"));
        var plates = page.FindAll(".pc-h2.pc-q").First(h => h.TextContent.Contains("plates")).Closest("section")!;
        Assert.Contains("A tiebreaker.", plates.TextContent);
        Assert.Null(plates.QuerySelector(".pc-plate-marker"));
        Assert.Null(plates.QuerySelector(".pc-plate-pool"));
        Assert.DoesNotContain("In the pools above", plates.TextContent);
    }

    [Fact]
    public void ThePlateBarPricesEveryPlateOnAD23InItsOwnColour()
    {
        // Seven segments (every plate above Rough Game), each sized by its bonus, wearing its
        // plate token and carrying what that plate is worth on a D23 — GetScore's number, not
        // the bonus: Base(23) 245 × 0.006 = +1.47 for a Marvelous Game.
        Population(null);
        var page = RenderPhoenix2();
        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        var plates = page.FindAll(".pc-h2.pc-q").First(h => h.TextContent.Contains("plates")).Closest("section")!;
        var segments = plates.QuerySelectorAll(".pc-plate-seg").ToArray();
        Assert.Equal(7, segments.Length);
        Assert.Equal(new[] { "FG", "TG", "MG", "SG", "EG", "UG", "PG" },
            segments.Select(s => s.QuerySelector(".pc-plate-seg-name")!.TextContent.Trim()));
        var widths = segments.Select(s => double.Parse(
            s.GetAttribute("style")!.Split(';').Single(p => p.StartsWith("width:"))["width:".Length..].TrimEnd('%'),
            CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(100, widths.Sum(), 1);
        // The last three plates step twice as far as the first four.
        Assert.Equal(widths[0] * 2, widths[6], 3);
        foreach (var segment in segments)
        {
            var plate = PhoenixPlateHelperMethods.ParseShorthand(segment.QuerySelector(".pc-plate-seg-name")!.TextContent.Trim());
            var expected = scoring.GetScore(ChartType.Double, 23, PhoenixLetterGrade.APlus.GetMinimumScoreFor(MixEnum.Phoenix2), plate)
                - scoring.GetScore(ChartType.Double, 23, PhoenixLetterGrade.APlus.GetMinimumScoreFor(MixEnum.Phoenix2), PhoenixPlate.RoughGame);
            Assert.Equal("+" + expected.ToString("0.00"), segment.QuerySelector(".pc-plate-seg-val")!.TextContent.Trim());
            Assert.Contains($"--plate-{plate.GetShorthand().ToLowerInvariant()}", segment.GetAttribute("style"));
        }
        Assert.Contains("+1.47", segments[2].TextContent);
        Assert.Contains("+4.90", segments[6].TextContent);
        // The ends and the list beneath (the phone's copy of the labels) say the same.
        Assert.Contains("+4.90 · Perfect", plates.QuerySelector(".pc-plate-ends")!.TextContent);
        Assert.Equal(7, plates.QuerySelectorAll(".pc-plate-list li").Length);
        Assert.Contains("Marvelous Game +1.47", plates.QuerySelector(".pc-plate-list")!.TextContent.Replace("\n", " "));
        // And the answer prices the ladder in the same currency: the two step sizes, one grade
        // rung and one level on the same chart for scale.
        var answer = plates.QuerySelector(".pc-answer")!.TextContent.Replace('\n', ' ');
        Assert.Contains("On a D23 each plate is worth +0.49 a step through Superb Game and +0.98 a step from Extreme Game up, so Rough Game to Perfect Game on one chart is +4.90.", answer);
        Assert.Contains("AA → AA+ is +4.90, and the same A+ one level higher is +6.75.", answer);
        Assert.Contains("worth about 1.5%", answer);
    }

    [Fact]
    public void ThePopulationSectionDrawsOneAverageSplitAndNamesTheBandsItCameFrom()
    {
        // Two drawable gems and one too thin: the split is one bar over the drawable pools, the
        // sentence says the split barely moves (31% at both ends), and the thin gem is named.
        Population(new PumbilityPoolCompositionRecord(MixEnum.Phoenix2, new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero), 28, new[]
        {
            Band("[P.B] SILVER", "[P.B] SILVER", 12_500, 15_000, 1, 13.4, 9_600, 4_500, 60, 49),
            Band("[P.B] GOLD", "[P.B] GOLD", 15_000, 16_000, 8, 15.9, 84_000, 38_400, 520, 36),
            Band("[P.B] DIAMOND", "[P.B] DIAMOND", 17_000, 18_000, 19, 20.6, 226_000, 104_000, 1_400, 40)
        }));
        var page = RenderPhoenix2();
        var section = page.FindAll(".pc-h2.pc-q").First(h => h.TextContent.Contains("push levels")).Closest("section")!;

        Assert.Single(section.QuerySelectorAll(".pmb-wpc-stack"));
        Assert.Contains("27 pools", section.QuerySelector(".pmb-wpc-total")!.TextContent);
        // Level 310,000 / total 454,320 = 68.2%; score 31.3%; plate 0.4%.
        var keys = section.QuerySelectorAll(".pmb-wpc-k-num").Select(k => k.TextContent.Trim()).ToArray();
        Assert.Equal("68.2%", keys[0]);
        Assert.Equal("31.3%", keys[1]);
        Assert.Equal("0.4%", keys[2]);
        var say = section.QuerySelector(".pmb-wpc-say")!.TextContent;
        Assert.Contains("barely moves", say);
        Assert.Contains("GOLD", say);
        Assert.Contains("DIAMOND", say);
        Assert.DoesNotContain("[P.B]", say);
        Assert.Contains("Push levels.", section.QuerySelector(".pc-answer")!.TextContent);
        Assert.Contains("not enough players yet: SILVER", section.QuerySelector(".pc-pop-bands")!.TextContent);
        Assert.Contains("Early days: 28 full pools", section.QuerySelector(".pc-answer")!.TextContent);

        // The plate bar marks where those pools sit, priced on the D23 like the plates are: the
        // base-weighted mean bonus is 1,920 / 310,000 = 0.0062, which is +1.5 on a D23 and
        // nearest a Marvelous Game. The answer quotes their plate share.
        var plates = page.FindAll(".pc-h2.pc-q").First(h => h.TextContent.Contains("plates")).Closest("section")!;
        var marker = plates.QuerySelector(".pc-plate-marker")!;
        Assert.Contains("left:30.97%", marker.GetAttribute("style"));
        Assert.Contains("the pools above average +1.5 — about a Marvelous Game", plates.QuerySelector(".pc-plate-pool")!.TextContent);
        Assert.Contains("In the pools above, plates carry 0.4% of the number.", plates.TextContent);
    }

    [Fact]
    public void WhenTheSplitMovesBetweenBandsTheSentenceSaysSoInstead()
    {
        // Phoenix-shaped data: 11% score at the bottom, 27% at the top — "barely moves" would be a lie.
        Population(new PumbilityPoolCompositionRecord(MixEnum.Phoenix, new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero), 300, new[]
        {
            Band("lt20k", null, 0, 20_000, 260, 14.4, 890_000, 110_000, 0, 14),
            Band("80k+", null, 80_000, null, 22, 24.6, 730_000, 270_000, 0, 47)
        }));
        var page = RenderPhoenix();
        var section = page.FindAll(".pc-h2.pc-q").First(h => h.TextContent.Contains("push levels")).Closest("section")!;
        var say = section.QuerySelector(".pmb-wpc-say")!.TextContent;
        Assert.Contains("It moves", say);
        Assert.Contains("under 20,000", say);
        Assert.Contains("80,000 and up", say);
        Assert.Contains("14 → 47 of fifty", say);
        Assert.Contains("Levels first, then scores.", section.QuerySelector(".pc-answer")!.TextContent);
        // Phoenix has no plate term: the plate key says so and the plate answer is "Nothing."
        Assert.Contains("this mix's formula has no plate term", section.TextContent);
        var plates = page.FindAll(".pc-h2.pc-q").First(h => h.TextContent.Contains("plates")).Closest("section")!;
        Assert.Contains("Nothing.", plates.TextContent);
        Assert.Null(plates.QuerySelector(".pc-plate-track"));
    }
}
