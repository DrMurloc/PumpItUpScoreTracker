using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Breakdown page's last block (docs/design/pumbility-overhaul.md §3.11, D57): the curve,
///     then the control row, then the fifty; the density and the folds are this page's own
///     settings; the identity chips are read for the fifty on screen and nothing more.
/// </summary>
public sealed class PumbilityPoolSectionTests : ComponentTestBase
{
    private static readonly Guid Me = Guid.NewGuid();

    public PumbilityPoolSectionTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User)
            .Returns(new User(Me, "Me", true, null, new Uri("https://piu.test/me.png"), null));
        Services.AddSingleton(Mock.Of<IDateTimeOffsetAccessor>(c =>
            c.Now == new DateTimeOffset(2026, 9, 5, 0, 0, 0, TimeSpan.Zero)));
        Mediator.Setup(m => m.Send(It.IsAny<GetChartIdentityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ChartIdentityRecord>());
        // The cards gate their tooltips on RendererInfo; declare the render world so bUnit can supply it.
        this.RenderInteractive();
    }

    [Fact]
    public void TheBlockIsTheCurveThenTheControlsThenTheFifty()
    {
        var f = new Fixture(poolSize: 3);

        var cut = RenderComponent<PumbilityPoolSection>(p => p.Add(x => x.Page, f.Page).Add(x => x.Charts, f.Charts));

        Assert.Equal("Your top 50", cut.Find(".pmb-block-title").TextContent.Trim());
        var curve = cut.Markup.IndexOf("pmb-curve", StringComparison.Ordinal);
        var controls = cut.Markup.IndexOf("pool-controls", StringComparison.Ordinal);
        var card = cut.Markup.IndexOf("tier-chart-card", StringComparison.Ordinal);
        Assert.True(curve >= 0 && curve < controls && controls < card, "curve, then controls, then the fifty");
        Assert.Equal(3, cut.FindAll(".tier-chart-card").Count);
        // Download and the density trio at the row's end; nothing on its left — no grouping to
        // choose, no energy to read, no switch.
        Assert.NotNull(cut.Find("[data-testid=pool-download]"));
        foreach (var density in new[] { "Comfortable", "Compact", "Table" })
            Assert.NotNull(cut.Find($"[data-testid=pool-controls] button[aria-label={density}]"));
        Assert.Empty(cut.FindAll("[data-testid=pool-controls] .mud-select"));
        Assert.Empty(cut.FindAll("[data-testid=pool-controls] .mud-switch"));
    }

    [Fact]
    public async Task ADensityPickIsRememberedForThisPage()
    {
        var f = new Fixture(poolSize: 2);
        var cut = RenderComponent<PumbilityPoolSection>(p => p.Add(x => x.Page, f.Page).Add(x => x.Charts, f.Charts));

        await cut.Find("[data-testid=pool-controls] button[aria-label=Compact]").ClickAsync(new MouseEventArgs());

        Assert.NotEmpty(cut.FindAll(".tier-chart-card-compact"));
        // Per page (UX rule 5): the Breakdown page's own key, never Play's.
        Mock.Get(Services.GetRequiredService<IUiSettingsAccessor>())
            .Verify(s => s.SetSetting(PumbilityPoolSection.DensitySettingKey, "Compact", It.IsAny<CancellationToken>()),
                Times.Once);
    }

    [Fact]
    public void TheIdentityChipsAreReadForTheFiftyOnScreenOnly()
    {
        // Seven charts in the catalog, three in the pool: the read names the three.
        var f = new Fixture(poolSize: 3, extraCharts: 4);

        var cut = RenderComponent<PumbilityPoolSection>(p => p.Add(x => x.Page, f.Page).Add(x => x.Charts, f.Charts));

        cut.WaitForAssertion(() => Mediator.Verify(
            m => m.Send(It.Is<GetChartIdentityQuery>(q => q.ChartIds.Count == 3 && q.Mix == MixEnum.Phoenix2),
                It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public void AnEmptyPoolShowsTheEmptyStateAndNoControls()
    {
        var f = new Fixture(poolSize: 0);

        var cut = RenderComponent<PumbilityPoolSection>(p => p.Add(x => x.Page, f.Page).Add(x => x.Charts, f.Charts));

        Assert.Empty(cut.FindAll("[data-testid=pool-controls]"));
        Assert.Contains("Nothing in your pool yet", cut.Find("[data-testid=ppl-empty]").TextContent);
        Mediator.Verify(m => m.Send(It.IsAny<GetChartIdentityQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ASavedFoldAppliesEvenWhenTheSettingLandsAfterTheFirstPaint()
    {
        // The settings accessor is a database round trip, so the block paints once before it
        // answers; the list has to take the folds when they arrive rather than keep its first
        // copy (bug check, 2026-09-05). Two charts a sigma apart band into two sections.
        var folds = new TaskCompletionSource<string?>();
        Mock.Get(Services.GetRequiredService<IUiSettingsAccessor>())
            .Setup(s => s.GetSetting(PumbilityPoolSection.CollapsedSettingKey, It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .Returns(folds.Task);
        var f = new Fixture(poolSize: 2);

        var cut = RenderComponent<PumbilityPoolSection>(p => p.Add(x => x.Page, f.Page).Add(x => x.Charts, f.Charts));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".tier-section-body").Count));
        folds.SetResult("VeryEasy");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".tier-section-body")));
        Assert.Empty(cut.FindAll("[data-testid=ppl-section-VeryEasy] .tier-section-body"));
        Assert.NotEmpty(cut.FindAll("[data-testid=ppl-section-Hard] .tier-section-body"));
    }

    // ------------------------------------------------------------------ fixture

    private sealed class Fixture
    {
        public Fixture(int poolSize, int extraCharts = 0)
        {
            var pool = new List<PoolEntry>();
            for (var i = 0; i < poolSize; i++)
            {
                var chart = NewChart($"Pool {i + 1}");
                pool.Add(new PoolEntry(i + 1, chart.Id, 966_887 - i * 1_000, PhoenixPlate.MarvelousGame, false,
                    DateTimeOffset.MinValue, 400 - i * 3));
            }

            for (var i = 0; i < extraCharts; i++) NewChart($"Elsewhere {i + 1}");

            Page = new PumbilityPageRecord(MixEnum.Phoenix2, null, pool.Sum(p => p.Value), null, null,
                pool, Array.Empty<PoolEntry>(), Array.Empty<PumbilityTarget>());
        }

        public Dictionary<Guid, Chart> Charts { get; } = new();

        public PumbilityPageRecord Page { get; }

        private Chart NewChart(string name)
        {
            var chart = new Chart(Guid.NewGuid(), MixEnum.Phoenix2,
                new Song(name, SongType.Arcade, new Uri("https://piu.test/i.png"), TimeSpan.FromMinutes(2), "Artist", 180),
                ChartType.Single, 21, MixEnum.Phoenix2, null, null);
            Charts[chart.Id] = chart;
            return chart;
        }
    }
}
