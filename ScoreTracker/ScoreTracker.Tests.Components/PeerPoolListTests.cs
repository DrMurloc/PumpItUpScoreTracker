using Bunit;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Enums;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The peers' pools in the tier list's shape (docs/design/pumbility-overhaul.md §3.10): the
///     two groupings, the gains cut, the jacket that carries only the gain, the borders and their
///     precedence, and the Compact marks with their words.
/// </summary>
public sealed class PeerPoolListTests : ComponentTestBase
{
    // DifficultyBubble gates its tooltip on RendererInfo.IsInteractive; every card carries one.
    public PeerPoolListTests() => this.RenderInteractive();

    [Fact]
    public void PrevalenceGroupsIntoTheLensTiersWithSlimAndPoorFolded()
    {
        var f = new Fixture()
            .Held("Staple", TierListCategory.Overrated, holders: 17, points: 550, mine: null)
            .Held("Solid", TierListCategory.Easy, holders: 8, points: 200, mine: 966_887, myRank: 20)
            .Held("Slim", TierListCategory.VeryHard, holders: 2, points: 30, mine: null)
            .Held("Poor", TierListCategory.Underrated, holders: 1, points: 3, mine: null);

        var cut = RenderComponent<PeerPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts)
            .Add(x => x.Density, UiDensity.Comfortable));

        var names = cut.FindAll(".tier-section-name").Select(n => n.TextContent).ToArray();
        Assert.Equal(new[] { "Staple", "Solid", "Slim", "Poor" }, names);
        // Slim and Poor start folded (D34): headers present, bodies absent.
        Assert.Equal(2, cut.FindAll(".tier-section-body").Count);
        // No "X of Y in your pool" under a tier — it read as a claim about the tier (owner, field test).
        Assert.Empty(cut.FindAll(".tier-section-stat"));
        // No X/Y corner on the jacket in either density (D38); the count is the caption.
        Assert.Empty(cut.FindAll(".tier-chart-card-corner"));
        Assert.Contains("17 of 23 peers", cut.Markup);
        Assert.Contains("In your pool #20", cut.Markup);
    }

    [Fact]
    public void TheJacketCarriesTheGainOnlyWhenTheChartPaysAndTheProjectedGradeInCompact()
    {
        var f = new Fixture()
            .Held("Pays", TierListCategory.Overrated, holders: 12, points: 500, mine: null, gain: 18.4, projected: 987_475)
            .Held("Free", TierListCategory.Overrated, holders: 10, points: 450, mine: null);

        var comfortable = RenderComponent<PeerPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts)
            .Add(x => x.Gains, f.Gains).Add(x => x.Density, UiDensity.Comfortable));
        var corners = comfortable.FindAll(".tier-chart-card-corner");
        Assert.Single(corners);
        Assert.Contains("pmb-corner-gain", corners[0].ClassName);
        Assert.Equal("+18", corners[0].TextContent.Trim());

        var compact = RenderComponent<PeerPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts)
            .Add(x => x.Gains, f.Gains).Add(x => x.Density, UiDensity.Compact));
        Assert.Equal(2, compact.FindAll(".tier-chart-card-compact").Count);
        // The paying tile: the gain in one bottom corner, the projected grade in the other.
        Assert.Single(compact.FindAll(".tier-chart-card-compact-grade.pmb-corner-gain"));
        Assert.Single(compact.FindAll(".tier-chart-card-corner-start.pmb-corner-gain"));
    }

    [Fact]
    public void TheVariabilityDotIsCompactOnlyAndTheWordAlwaysPrints()
    {
        var f = new Fixture()
            .Held("Split", TierListCategory.Overrated, holders: 12, points: 500, mine: null,
                median: 985_000, variability: PeerVariabilityLevel.Split);

        var comfortable = RenderComponent<PeerPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts)
            .Add(x => x.Density, UiDensity.Comfortable));
        Assert.Empty(comfortable.FindAll(".tier-chart-card-lens-dot"));
        var meter = comfortable.Find("[data-testid=pmb-vary]");
        Assert.Contains("Split", meter.TextContent);
        Assert.Equal(4, meter.QuerySelectorAll(".pmb-vary-dots i.on").Length);

        var compact = RenderComponent<PeerPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts)
            .Add(x => x.Density, UiDensity.Compact));
        var dot = compact.Find(".tier-chart-card-lens-dot");
        Assert.Contains("--vary-4", dot.GetAttribute("style"));
        Assert.Equal("Split", dot.GetAttribute("title"));
    }

    [Fact]
    public void ProjectedGainsBandsThePayingChartsAndInterleavesCarriedRows()
    {
        var f = new Fixture()
            .Held("Big", TierListCategory.Overrated, holders: 12, points: 500, mine: null, gain: 18.4, projected: 987_475)
            .Held("Small", TierListCategory.Easy, holders: 5, points: 100, mine: null, gain: 3.2, projected: 975_000)
            .Held("None", TierListCategory.Medium, holders: 4, points: 60, mine: null)
            .CarriedUnheld("Further", gain: 24.4, projected: 980_127);

        var cut = RenderComponent<PeerPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts)
            .Add(x => x.Gains, f.Gains).Add(x => x.GroupBy, PeerGrouping.ProjectedGains).Add(x => x.Density, UiDensity.Comfortable));

        var names = cut.FindAll(".tier-section-name").Select(n => n.TextContent).ToArray();
        Assert.Equal(new[] { "+15 to +25", "+2 to +5" }, names);
        // The band holds the carried row above the peer row, by gain, and the non-paying chart is gone.
        var firstBand = cut.FindAll(".tier-chart-card-name").Select(n => n.TextContent).ToArray();
        Assert.Equal(new[] { "Further", "Big", "Small" }, firstBand);
        Assert.DoesNotContain("None", cut.Markup);
        // The carried row wears the other-mix ring and its Phoenix 1 line; the caption names the tier.
        Assert.Single(cut.FindAll(".tier-chart-card-other-mix"));
        Assert.Contains("Phoenix 1:", cut.Markup);
        Assert.Contains("Staple · 12 of 23 peers", cut.Markup);
    }

    [Fact]
    public void TheGainsCutUnderPrevalenceKeepsTheTiersAndHidesYoursAloneAndTheCarriedRows()
    {
        var f = new Fixture()
            .Held("Pays", TierListCategory.Overrated, holders: 12, points: 500, mine: null, gain: 18.4, projected: 987_475)
            .Held("Free", TierListCategory.Overrated, holders: 10, points: 450, mine: null)
            .CarriedUnheld("Further", gain: 24.4, projected: 980_127)
            .Alone("Paradoxx", myRank: 3, score: 855_394);

        var whole = RenderComponent<PeerPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts)
            .Add(x => x.Gains, f.Gains).Add(x => x.Density, UiDensity.Comfortable));
        Assert.Equal(new[] { "Staple", "Yours alone" }, whole.FindAll(".tier-section-name").Select(n => n.TextContent).ToArray());
        Assert.DoesNotContain("Further", whole.Markup);

        var cut = RenderComponent<PeerPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts)
            .Add(x => x.Gains, f.Gains).Add(x => x.GainsOnly, true).Add(x => x.Density, UiDensity.Comfortable));
        // Prevalence is what the peers hold: a carried row nobody holds has none, and its own
        // switch does not even show under this grouping (owner, field test round one).
        Assert.Equal(new[] { "Staple" }, cut.FindAll(".tier-section-name").Select(n => n.TextContent).ToArray());
        Assert.DoesNotContain("Carried from Phoenix 1", cut.Markup);
        Assert.DoesNotContain("Free", cut.Markup);
        Assert.DoesNotContain("Paradoxx", cut.Markup);
    }

    [Fact]
    public void TheBorderPrecedenceIsTheTierListsPassedThenToDoThenCarried()
    {
        var f = new Fixture()
            .Held("Passed", TierListCategory.Overrated, holders: 12, points: 500, mine: 980_205, myRank: 10)
            .Held("Wanted", TierListCategory.Overrated, holders: 10, points: 450, mine: null)
            .CarriedUnheld("Further", gain: 24.4, projected: 980_127);
        var todos = new HashSet<Guid> { f.Id("Wanted"), f.Id("Further") };

        var cut = RenderComponent<PeerPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts)
            .Add(x => x.Gains, f.Gains).Add(x => x.GroupBy, PeerGrouping.ProjectedGains).Add(x => x.ShowToDo, true)
            .Add(x => x.ToDos, todos).Add(x => x.Density, UiDensity.Compact));

        // Further is carried AND To-Do: To-Do wins the ring, as it does on the tier list.
        Assert.Single(cut.FindAll(".tier-card-grid .tier-chart-card-todo"));
        Assert.Empty(cut.FindAll(".tier-card-grid .tier-chart-card-other-mix"));
        // Passed does not pay here (no gain), so it is not on the gains view at all.
        Assert.DoesNotContain("Passed", cut.Markup);
    }

    [Fact]
    public void TableCarriesTheColumnsAndTheWeightedSumInTheTooltip()
    {
        var f = new Fixture()
            .Held("Row", TierListCategory.Overrated, holders: 17, points: 550, mine: 966_887, myRank: 20,
                median: 985_000, variability: PeerVariabilityLevel.Mixed, gain: 10.2, projected: 985_000, percentile: 0.11);

        var cut = RenderComponent<PeerPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts)
            .Add(x => x.Gains, f.Gains).Add(x => x.Density, UiDensity.Table));

        var headers = cut.FindAll("thead th").Select(h => h.TextContent.Trim()).ToArray();
        Assert.Contains("Peers", headers);
        Assert.Contains("Peers' median", headers);
        Assert.Contains("Variability", headers);
        Assert.Contains("Gain", headers);
        Assert.Contains("Better Than", headers);
        Assert.Contains("Your pool", headers);
        var row = cut.Find("tbody tr");
        Assert.Contains("17 of 23 peers", row.TextContent);
        Assert.Contains("Weighted sum: 550", row.QuerySelector("td[title]")!.GetAttribute("title"));
        Assert.Contains("11%", row.TextContent);
        Assert.Contains("In your pool #20", row.TextContent);
        Assert.Contains("tier-row-pass", row.ClassName);
    }

    [Fact]
    public void YourTop50BandsThePoolByWhatEachChartIsWorthWithThePeersDataOnEveryRow()
    {
        // D44 + field test round one: the pool is banded by value the way every other list bands,
        // in the page's own tier names — no waiting room, no place-ordered slab. The peers' entry
        // rides where one exists; a chart no peer holds says so.
        var f = new Fixture()
            .Held("Shared", TierListCategory.Easy, holders: 8, points: 200, mine: 966_887, myRank: 2,
                median: 985_000, variability: PeerVariabilityLevel.Mixed, gain: 10.2, projected: 985_000)
            .Alone("Mine", myRank: 1, score: 990_000)
            .Held("Unplayed", TierListCategory.Overrated, holders: 17, points: 550, mine: null)
            .InPool("Mine", place: 1, value: 460).InPool("Shared", place: 2, value: 300);

        var cut = RenderComponent<PeerPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts)
            .Add(x => x.Gains, f.Gains).Add(x => x.PoolRecord, f.PoolRecord()).Add(x => x.GroupBy, PeerGrouping.YourTop50)
            .Add(x => x.Density, UiDensity.Comfortable));

        // The processor's own cuts over 460 and 300: mean 380, sigma 80, so one lands a sigma above
        // (Strong) and one half a sigma below (Modest) — derived by its rule, not chosen here.
        Assert.Equal(new[] { "Strong", "Modest" }, cut.FindAll(".tier-section-name").Select(n => n.TextContent).ToArray());
        Assert.Empty(cut.FindAll(".tier-section-stat"));
        var top = cut.Find("[data-testid=ppl-section-VeryEasy] .tier-chart-card");
        Assert.Contains("Mine", top.TextContent);
        Assert.Contains("No peer holds it", top.TextContent);
        Assert.Contains("In your pool #1 · 460.00", top.TextContent);
        var bottom = cut.Find("[data-testid=ppl-section-Hard] .tier-chart-card");
        Assert.Contains("Shared", bottom.TextContent);
        Assert.Contains("8 of 23 peers", bottom.TextContent);
        // The pool is the pool: a chart the peers hold that you have never played is not in it.
        Assert.DoesNotContain("Unplayed", cut.Markup);
        Assert.DoesNotContain("Waiting room", cut.Markup);
    }

    [Fact]
    public void YourTop50CompactPutsYourGradeOnOneCornerAndWhatItIsWorthOnTheOther()
    {
        // Owner, field test round one. The badges are the page's own corner treatment, so a pool
        // card sits beside a peers card without looking like a different component.
        var f = new Fixture()
            .Held("Shared", TierListCategory.Easy, holders: 8, points: 200, mine: 966_887, myRank: 1,
                median: 985_000, gain: 10.2, projected: 985_000)
            .InPool("Shared", place: 1, value: 398.25);

        var compact = RenderComponent<PeerPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts)
            .Add(x => x.Gains, f.Gains).Add(x => x.PoolRecord, f.PoolRecord()).Add(x => x.GroupBy, PeerGrouping.YourTop50)
            .Add(x => x.Density, UiDensity.Compact));

        var start = compact.Find(".tier-chart-card-corner-start");
        Assert.Contains("pmb-corner-gain", start.ClassName);
        Assert.NotNull(start.QuerySelector("img"));
        var end = compact.Find(".tier-chart-card-compact-grade.tier-chart-card-corner");
        Assert.Equal("398.25", end.TextContent.Trim());
        Assert.Contains("pmb-corner-gain", end.ClassName);
        // No prevalence stripe anywhere on this page any more (owner, field test round one).
        Assert.Empty(compact.FindAll(".tier-chart-card-stripe"));

        // Comfortable keeps the gain badge it wears everywhere else; the body prints place and value.
        var comfortable = RenderComponent<PeerPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts)
            .Add(x => x.Gains, f.Gains).Add(x => x.PoolRecord, f.PoolRecord()).Add(x => x.GroupBy, PeerGrouping.YourTop50)
            .Add(x => x.Density, UiDensity.Comfortable));
        Assert.StartsWith("+10", comfortable.Find(".tier-chart-card-corner").TextContent.Trim());
        Assert.Contains("In your pool #1 · 398.25", comfortable.Markup);
    }

    [Fact]
    public void YourTop50WithNoPoolRecordOrAnEmptyPoolSaysSo()
    {
        var f = new Fixture().Held("Shared", TierListCategory.Easy, holders: 8, points: 200, mine: null);

        var cut = RenderComponent<PeerPoolList>(p => p.Add(x => x.Page, f.Page()).Add(x => x.Charts, f.Charts)
            .Add(x => x.PoolRecord, f.PoolRecord()).Add(x => x.GroupBy, PeerGrouping.YourTop50));

        Assert.Contains("Nothing in your pool yet", cut.Find("[data-testid=ppl-empty]").TextContent);
    }

    // ------------------------------------------------------------------ fixture

    private sealed class Fixture
    {
        private readonly List<PeerPoolEntry> _entries = new();
        private readonly List<PeerAloneEntry> _alone = new();
        private readonly Dictionary<Guid, PumbilityTarget> _gains = new();
        private readonly Dictionary<string, Guid> _ids = new();
        private readonly List<PoolEntry> _pool = new();

        public Dictionary<Guid, Chart> Charts { get; } = new();

        public IReadOnlyDictionary<Guid, PumbilityTarget> Gains => _gains;

        public Guid Id(string name) => _ids[name];

        public Fixture Held(string name, TierListCategory tier, int holders, int points, int? mine, int? myRank = null,
            int? median = null, PeerVariabilityLevel? variability = null, double? gain = null, int? projected = null,
            double? percentile = null)
        {
            var chart = NewChart(name);
            _entries.Add(new PeerPoolEntry(chart.Id, ChartType.Single, holders, 23, points, tier, _entries.Count,
                holders, median, median, median, variability, myRank, mine, mine == null ? null : PhoenixPlate.MarvelousGame,
                percentile));
            if (gain is { } g)
                _gains[chart.Id] = new PumbilityTarget(chart.Id, projected ?? 980_000, g, mine, false, null);
            return this;
        }

        public Fixture CarriedUnheld(string name, double gain, int projected)
        {
            var chart = NewChart(name);
            _gains[chart.Id] = new PumbilityTarget(chart.Id, projected, gain, null, false, null, TargetSource.Phoenix1);
            return this;
        }

        public Fixture Alone(string name, int myRank, int score)
        {
            var chart = NewChart(name);
            _alone.Add(new PeerAloneEntry(chart.Id, ChartType.Single, myRank, score, PhoenixPlate.RoughGame, 300));
            return this;
        }

        /// <summary>A chart already named in this fixture, at a place in the frame's pool with a value.</summary>
        public Fixture InPool(string name, int place, double value)
        {
            _pool.Add(new PoolEntry(place, _ids[name], 966_887, PhoenixPlate.MarvelousGame, false, DateTimeOffset.MinValue, value));
            return this;
        }

        /// <summary>The frame's record: the pool this fixture declared, nothing else.</summary>
        public PumbilityPageRecord PoolRecord()
        {
            return new PumbilityPageRecord(MixEnum.Phoenix2, ChartType.Single, _pool.Sum(p => p.Value), null, null,
                _pool.OrderBy(p => p.Place).ToArray(), Array.Empty<PoolEntry>(), Array.Empty<PumbilityTarget>());
        }

        public PumbilityPeersPageRecord Page()
        {
            return new PumbilityPeersPageRecord(MixEnum.Phoenix2, ChartType.Single,
                new Dictionary<ChartType, PeerGroup> { [ChartType.Single] = PeerGroup.Pumbility(24, 23, 50) },
                _entries, _alone, Array.Empty<PeerRosterEntry>(), 0, null, new Dictionary<ChartType, PeerCompare>());
        }

        private Chart NewChart(string name)
        {
            var chart = new Chart(Guid.NewGuid(), MixEnum.Phoenix2,
                new Song(name, SongType.Arcade, new Uri("https://piu.test/i.png"), TimeSpan.FromMinutes(2), "Artist", 180),
                ChartType.Single, 21, MixEnum.Phoenix2, null, null, new HashSet<Skill>());
            Charts[chart.Id] = chart;
            _ids[name] = chart.Id;
            return chart;
        }
    }
}
