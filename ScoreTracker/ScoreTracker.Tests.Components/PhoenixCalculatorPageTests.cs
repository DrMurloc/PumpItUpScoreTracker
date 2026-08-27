using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Bunit;
using Moq;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.Tools;
using ScoreTracker.Web.Services;
using Xunit;
using Chart = ScoreTracker.SharedKernel.Models.Chart;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Phoenix score page as static markup (docs/design/phoenix-score-calculator.md).
///     The arithmetic is pinned in DomainTests; these pin that the page prints exactly what
///     the engine says — every ladder cell equals the mix's floor table, every budget cell
///     equals the formula's own answer — and that each mix gets its own shape.
/// </summary>
public sealed class PhoenixCalculatorPageTests : ComponentTestBase
{
    public PhoenixCalculatorPageTests()
    {
        // The page is static SSR; DifficultyBubble gates its tooltip on RendererInfo, so the
        // test declares the same world the real page renders in.
        Renderer.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Static", false));
    }

    private static Chart Make(ChartType type, int level, MixEnum mix, int? noteCount)
    {
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix,
            new Song("Song", SongType.Arcade, new Uri("https://piu.test/art.png"), TimeSpan.FromMinutes(2), "Doin",
                Bpm.From(180, 180)),
            type, level, mix, null, noteCount);
    }

    private void Setup(MixEnum mix,
        Chart[]? charts = null,
        LevelScorePopulation[]? population = null,
        GradeJudgementSpread[]? spreads = null,
        HoldTickProfile? holds = null)
    {
        Mediator.Setup(m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == mix), It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts ?? new[]
            {
                Make(ChartType.Single, 18, mix, 1000),
                Make(ChartType.Single, 18, mix, 1200),
                Make(ChartType.Double, 20, mix, 1100)
            });
        if (mix != MixEnum.Phoenix)
            Mediator.Setup(m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == MixEnum.Phoenix),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Chart>());
        Mediator.Setup(m => m.Send(new GetScorePopulationQuery(mix), It.IsAny<CancellationToken>()))
            .ReturnsAsync(population ?? new[] { new LevelScorePopulation(18, 100, 2, 10, 20, 30, 20, 10, 8) });
        Mediator.Setup(m => m.Send(new GetJudgementSpreadsQuery(mix), It.IsAny<CancellationToken>()))
            .ReturnsAsync(spreads ?? new[]
            {
                new GradeJudgementSpread(PhoenixLetterGrade.S, 120, 959.0, 24.6, 5.2, 3.1, 8.1, 462.7, 100),
                new GradeJudgementSpread(PhoenixLetterGrade.AA, 12, 864.7, 80.7, 23.2, 11.1, 20.3, 270.8, 10)
            });
        Mediator.Setup(m => m.Send(new GetHoldTickProfileQuery(mix), It.IsAny<CancellationToken>()))
            .ReturnsAsync(holds ?? new HoldTickProfile(
                new[] { new HoldTickLevelStat(18, 40, .48, .31, .62) },
                new[]
                {
                    new HoldTickChartStat(Guid.NewGuid(), "Ugly Dee", ChartType.Double, 17, 71, 71, 1.0)
                },
                new[]
                {
                    new HoldTickChartStat(Guid.NewGuid(), "Bee", ChartType.Single, 17, 830, 50, .06)
                },
                41));
    }

    private IRenderedComponent<PhoenixCalculator> Render(MixEnum mix)
    {
        Setup(mix);
        return RenderComponent<PhoenixCalculator>(p =>
            p.Add(x => x.MixSlug, ChartSlugs.MixSlug(mix)));
    }

    [Fact]
    public void EveryLadderCellEqualsTheMixesOwnFloorTable()
    {
        foreach (var mix in new[] { MixEnum.Phoenix, MixEnum.Phoenix2 })
        {
            var page = Render(mix);
            var rows = page.FindAll(".sc-table tr[data-sc-grade]")
                .Where(r => r.Children.Length == 4)
                .ToArray();
            Assert.Equal(16, rows.Length);
            foreach (var row in rows)
            {
                var grade = PhoenixLetterGradeHelperMethods.TryParse(
                    row.GetAttribute("data-sc-grade")!)!.Value;
                // The letter renders as the site's art, not text.
                Assert.Contains("letters/", row.QuerySelector("td img")!.GetAttribute("src"));
                Assert.Equal(((int)grade.GetMinimumScoreFor(mix)).ToString("N0"),
                    row.Children[1].TextContent.Trim());
                Assert.Equal(((int)grade.GetMaximumScoreFor(mix)).ToString("N0"),
                    row.Children[2].TextContent.Trim());
            }
        }
    }

    [Fact]
    public void EveryBudgetCellEqualsTheFormulasOwnAnswer()
    {
        var page = Render(MixEnum.Phoenix2);

        foreach (var cell in page.FindAll("[data-sc-budget]"))
        {
            var grade = PhoenixLetterGradeHelperMethods.TryParse(cell.GetAttribute("data-sc-budget")!)!.Value;
            Assert.Equal(
                Web.Services.ScoreCalculator.ScoreCalculatorModel
                    .GreatsAllowedFor(grade, MixEnum.Phoenix2, 1000).ToString("N0"),
                cell.TextContent.Trim());
        }
    }

    [Fact]
    public void TheConstantsBlockCarriesBothMixesFloorsFromTheEnum()
    {
        var page = Render(MixEnum.Phoenix2);

        var json = page.Find("[data-sc-constants]").TextContent;
        using var constants = JsonDocument.Parse(json);
        foreach (var mix in new[] { MixEnum.Phoenix, MixEnum.Phoenix2 })
        {
            var floors = constants.RootElement.GetProperty("floors").GetProperty(mix.ToString());
            Assert.Equal(16, floors.GetArrayLength());
            foreach (var entry in floors.EnumerateArray())
            {
                var grade = PhoenixLetterGradeHelperMethods.TryParse(entry.GetProperty("grade").GetString()!)!.Value;
                Assert.Equal((int)grade.GetMinimumScoreFor(mix), entry.GetProperty("floor").GetInt32());
            }
        }
    }

    [Fact]
    public void TheMovedStripRendersOnPhoenixTwoAndTheCreditsOnPhoenix()
    {
        var phoenix2 = Render(MixEnum.Phoenix2);
        Assert.NotEmpty(phoenix2.FindAll(".sc-moved"));
        Assert.Empty(phoenix2.FindAll(".sc-credit"));

        var phoenix = Render(MixEnum.Phoenix);
        Assert.Empty(phoenix.FindAll(".sc-moved"));
        Assert.NotEmpty(phoenix.FindAll(".sc-credit"));
    }

    [Fact]
    public void BothNoteChartTypesRenderWithDoublesHidden()
    {
        var page = Render(MixEnum.Phoenix2);

        var blocks = page.FindAll("[data-sc-type]");
        Assert.Equal(new[] { "Single", "Double" }, blocks.Select(b => b.GetAttribute("data-sc-type")));
        Assert.False(blocks[0].HasAttribute("hidden"));
        Assert.True(blocks[1].HasAttribute("hidden"));
    }

    [Fact]
    public void SpreadRowsUnderTheDisplayGateStayOut()
    {
        var page = Render(MixEnum.Phoenix2);

        var grades = page.FindAll("[data-sc-spread-grade]")
            .Select(r => r.GetAttribute("data-sc-spread-grade")).ToArray();
        Assert.Equal(new[] { "S" }, grades);
    }

    [Fact]
    public void ThinPopulationLevelsRenderTheEmptyState()
    {
        Setup(MixEnum.Phoenix2, population: new[] { new LevelScorePopulation(27, 5, 1, 1, 1, 1, 1, 0, 0) });
        var page = RenderComponent<PhoenixCalculator>(p => p.Add(x => x.MixSlug, "phoenix-2"));

        Assert.Empty(page.FindAll("svg[aria-label*='personal bests']"));
    }

    [Fact]
    public void TheLoadButtonAndDialogAreSignedInOnly()
    {
        CurrentUser.Setup(u => u.IsLoggedIn).Returns(false);
        var anonymous = Render(MixEnum.Phoenix2);
        Assert.Empty(anonymous.FindAll("[data-sc-load]"));
        Assert.Empty(anonymous.FindAll("[data-sc-dialog]"));

        CurrentUser.Setup(u => u.IsLoggedIn).Returns(true);
        var signedIn = Render(MixEnum.Phoenix);
        Assert.NotEmpty(signedIn.FindAll("[data-sc-load]"));
        var list = signedIn.Find("[data-sc-play-list]");
        Assert.Contains("MyPlays?mix=Phoenix", list.GetAttribute("data-endpoint"));
    }

    [Fact]
    public void HoldExtremesAndTheEstimateNoteRender()
    {
        var page = Render(MixEnum.Phoenix2);

        Assert.Contains("Ugly Dee", page.Markup);
        Assert.Contains("passing-F ceiling", page.Markup);
        Assert.Contains("the numbers are estimates right now", page.Markup);
    }
}
