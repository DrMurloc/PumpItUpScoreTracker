using System;
using System.Collections.Generic;
using System.Linq;
using Bunit;
using ScoreTracker.Domain.Models;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Enums;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The fifty as a tier list on the Breakdown page (docs/design/pumbility-overhaul.md §3.11,
///     D57): banded by value in the pool's own vocabulary, the value on the jacket corner, the
///     place in the body, no peers' data anywhere, and nothing folded by default.
/// </summary>
public sealed class PumbilityPoolListTests : ComponentTestBase
{
    // DifficultyBubble gates its tooltip on RendererInfo.IsInteractive; every card carries one.
    public PumbilityPoolListTests() => this.RenderInteractive();

    [Fact]
    public void ThePoolIsBandedByWhatEachChartIsWorthInThePoolVocabularyWithNothingFolded()
    {
        var f = new Fixture().InPool("Mine", place: 1, value: 460).InPool("Shared", place: 2, value: 300);

        var cut = RenderComponent<PumbilityPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts)
            .Add(x => x.Density, UiDensity.Comfortable));

        // The processor's own cuts over 460 and 300: mean 380, sigma 80, so one lands a sigma above
        // and one half a sigma below — derived by its rule, not chosen here. The names are the pool
        // vocabulary (D46): these bands are what a chart is worth to you, not how many keep it.
        Assert.Equal(new[] { "Very High", "Low" }, cut.FindAll(".tier-section-name").Select(n => n.TextContent).ToArray());
        // Nothing folded: the bar is at the bottom, and that is the part a reader came for.
        Assert.Equal(2, cut.FindAll(".tier-section-body").Count);
        Assert.Empty(cut.FindAll(".tier-section-stat"));
        var top = cut.Find("[data-testid=ppl-section-VeryEasy] .tier-chart-card");
        Assert.Contains("Mine", top.TextContent);
        Assert.Contains("In your pool #1", top.TextContent);
        Assert.Equal("460.00", top.QuerySelector(".tier-chart-card-corner")!.TextContent.Trim());
        // Every pool row is a pass, and none of them says a word about peers or projections.
        Assert.Equal(2, cut.FindAll(".tier-chart-card-pass").Count);
        Assert.Empty(cut.FindAll(".pmb-peers-line"));
        Assert.All(cut.FindAll(".tier-chart-card-body"), body =>
        {
            Assert.DoesNotContain("peers", body.TextContent);
            Assert.DoesNotContain("Projected", body.TextContent);
        });
    }

    [Fact]
    public void CompactPutsYourGradeOnOneCornerAndWhatItIsWorthOnTheOther()
    {
        var f = new Fixture().InPool("Shared", place: 1, value: 398.25);

        var compact = RenderComponent<PumbilityPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts)
            .Add(x => x.Density, UiDensity.Compact));

        var start = compact.Find(".tier-chart-card-corner-start");
        Assert.Contains("pmb-corner-gain", start.ClassName);
        Assert.NotNull(start.QuerySelector("img"));
        var end = compact.Find(".tier-chart-card-compact-grade.tier-chart-card-corner");
        Assert.Equal("398.25", end.TextContent.Trim());
        // No prevalence stripe, no lens dot: the corner and the section already say all of it.
        Assert.Empty(compact.FindAll(".tier-chart-card-stripe"));
        Assert.Empty(compact.FindAll(".tier-chart-card-lens-dot"));

        // Comfortable keeps the value in the same corner; the body prints the place.
        var comfortable = RenderComponent<PumbilityPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts)
            .Add(x => x.Density, UiDensity.Comfortable));
        Assert.Equal("398.25", comfortable.Find(".tier-chart-card-corner").TextContent.Trim());
        Assert.Contains("In your pool #1", comfortable.Find(".pmb-pool-line").TextContent);
    }

    [Fact]
    public void TableCarriesPlaceScoreAndValueAndNoPeersColumns()
    {
        var f = new Fixture().InPool("Shared", place: 7, value: 351.5);

        var cut = RenderComponent<PumbilityPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts)
            .Add(x => x.Density, UiDensity.Table));

        var headers = cut.FindAll("thead th").Select(h => h.TextContent.Trim()).ToArray();
        Assert.Contains("#", headers);
        Assert.Contains("My Score", headers);
        Assert.Contains("Value", headers);
        Assert.DoesNotContain("Peers", headers);
        Assert.DoesNotContain("Projected", headers);
        Assert.DoesNotContain("Gain", headers);
        Assert.DoesNotContain("Better Than", headers);
        var row = cut.Find("tbody tr");
        Assert.Contains("tier-row-pass", row.ClassName);
        Assert.Equal("7", row.QuerySelector("td.pmb-num")!.TextContent.Trim());
        Assert.Equal("351.50", row.QuerySelector("td.pmb-val")!.TextContent.Trim());
    }

    [Fact]
    public void TheShareSectionsCarryThePoolsOwnValueOnEveryTile()
    {
        // The download prices the PUMBILITY chip off the tile's pool value, so the picture and the
        // page cannot disagree about a number.
        var f = new Fixture().InPool("Mine", place: 1, value: 460).InPool("Shared", place: 2, value: 300);

        var cut = RenderComponent<PumbilityPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts));

        var tiles = cut.Instance.ShareSections().SelectMany(s => s.Tiles).ToArray();
        Assert.Equal(new double?[] { 460, 300 }, tiles.Select(t => t.PoolValue).ToArray());
        Assert.All(tiles, t => Assert.Null(t.Gain));
    }

    [Fact]
    public void AnEmptyPoolSaysSo()
    {
        var f = new Fixture();

        var cut = RenderComponent<PumbilityPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts));

        Assert.Contains("Nothing in your pool yet", cut.Find("[data-testid=ppl-empty]").TextContent);
    }

    // ------------------------------------------------------------------ fixture

    [Fact]
    public void AStandingColoursTheScoreWithoutPuttingPeersInTheBody()
    {
        // Both halves matter. The score is the viewer’s own, so it wears where it stands among
        // the peers they chose and its popover can explain that — without a standing it painted
        // plain and the popover had nothing to say, which read as a page still loading. And the
        // body stays peer-free, because that is this list's whole decision (D57): the peers'
        // data lives on Play.
        var f = new Fixture().InPool("Mine", place: 1, value: 460);
        var chartId = f.Charts.Keys.Single();
        var standing = new PeerStanding(12, 8, 2, 0, 0, new[]
        {
            new PeerStandingSource(PeerSourceKind.Pumbility, null, null, false, false, 12, 8, 2, 0)
        }, null);

        var plain = RenderComponent<PumbilityPoolList>(p => p.Add(x => x.Page, f.Page())
            .Add(x => x.Charts, f.Charts).Add(x => x.Density, UiDensity.Comfortable));
        var coloured = RenderComponent<PumbilityPoolList>(p => p.Add(x => x.Page, f.Page())
            .Add(x => x.Charts, f.Charts).Add(x => x.Density, UiDensity.Comfortable)
            .Add(x => x.Standings, new Dictionary<Guid, PeerStanding> { [chartId] = standing }));

        Assert.NotEqual(plain.Find("[data-testid=peer-score]").GetAttribute("style"),
            coloured.Find("[data-testid=peer-score]").GetAttribute("style"));
        Assert.Empty(coloured.FindAll(".tier-chart-card-standing"));
        Assert.DoesNotContain("peers", coloured.Find(".tier-chart-card-body").TextContent);
    }

    /// <summary>
    ///     A popover source line offers that source’s board, and offering it means the page hears
    ///     about it. Without the delegate the popover still opens and still explains the colour,
    ///     but its rows go inert — a row that reads as a link and does nothing (owner field test,
    ///     2026-09-06). The delegate has to reach the score itself, which is what draws them.
    /// </summary>
    [Fact]
    public void ThePagesBoardHandlerReachesTheScoreThatDrawsTheSourceRows()
    {
        var f = new Fixture().InPool("Mine", place: 1, value: 460);
        var chartId = f.Charts.Keys.Single();
        var standing = new PeerStanding(12, 8, 2, 0, 0, new[]
        {
            new PeerStandingSource(PeerSourceKind.Pumbility, null, null, false, false, 12, 8, 2, 0)
        }, null);

        var cut = RenderComponent<PumbilityPoolList>(p => p.Add(x => x.Page, f.Page())
            .Add(x => x.Charts, f.Charts).Add(x => x.Density, UiDensity.Comfortable)
            .Add(x => x.Standings, new Dictionary<Guid, PeerStanding> { [chartId] = standing })
            .Add(x => x.OnOpenBoard, _ => { }));

        Assert.True(cut.FindComponent<PeerScore>().Instance.OnOpenBoard.HasDelegate);
    }

    private sealed class Fixture
    {
        private readonly List<PoolEntry> _pool = new();

        public Dictionary<Guid, Chart> Charts { get; } = new();

        /// <summary>A chart at a place in the frame's pool with a value; the score and plate are the same on every row.</summary>
        public Fixture InPool(string name, int place, double value)
        {
            var chart = new Chart(Guid.NewGuid(), MixEnum.Phoenix2,
                new Song(name, SongType.Arcade, new Uri("https://piu.test/i.png"), TimeSpan.FromMinutes(2), "Artist", 180),
                ChartType.Single, 21, MixEnum.Phoenix2, null, null);
            Charts[chart.Id] = chart;
            _pool.Add(new PoolEntry(place, chart.Id, 966_887, PhoenixPlate.MarvelousGame, false, DateTimeOffset.MinValue, value));
            return this;
        }

        /// <summary>The frame's record: the pool this fixture declared, nothing else.</summary>
        public PumbilityPageRecord Page()
        {
            var pool = _pool.OrderBy(p => p.Place).ToArray();
            return new PumbilityPageRecord(MixEnum.Phoenix2, ChartType.Single, pool.Sum(p => p.Value), null, null,
                pool, Array.Empty<PoolEntry>(), Array.Empty<PumbilityTarget>());
        }
    }
}
