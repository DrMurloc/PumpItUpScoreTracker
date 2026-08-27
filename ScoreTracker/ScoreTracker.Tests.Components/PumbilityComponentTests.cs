using Bunit;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Enums;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class PumbilityComponentTests : ComponentTestBase
{
    // SongImage and DifficultyBubble gate their tooltip on RendererInfo.IsInteractive, and
    // every surface here nests one. The page declares a circuit, so that is the real world.
    public PumbilityComponentTests() => this.RenderInteractive();

    // ------------------------------------------------------------------ hero

    [Fact]
    public void TheHeroLeadsWithTheNumberAndTheBarUnderIt()
    {
        var page = Page(poolSize: 50);

        var cut = RenderComponent<PumbilityHero>(p => p
            .Add(x => x.Page, page)
            .Add(x => x.Charts, page.Charts()));

        // Two decimals, the way piugame prints it — asserted through the same format the
        // component uses so this says "the hero shows the pool" and not "N2 is two decimals".
        Assert.Contains(page.Total.ToString("N2"), cut.Find(".pmb-hero-value").TextContent);
        Assert.Equal(page.Bar!.Value.ToString("N2"), cut.Find(".pmb-barcard-num").TextContent.Trim());
    }

    [Fact]
    public void AnUnfilledPoolExplainsWhyThereIsNoBarRatherThanPrintingZero()
    {
        var page = Page(poolSize: 12);

        var cut = RenderComponent<PumbilityHero>(p => p
            .Add(x => x.Page, page)
            .Add(x => x.Charts, page.Charts()));

        Assert.Empty(cut.FindAll(".pmb-barcard-num"));
        Assert.Contains("12 of 50", cut.Find(".pmb-barcard-none").TextContent);
    }

    [Fact]
    public void ThePoolSelectorOnlyAppearsWhenThereIsMoreThanOnePool()
    {
        var page = Page(poolSize: 50);

        var without = RenderComponent<PumbilityHero>(p => p
            .Add(x => x.Page, page).Add(x => x.Charts, page.Charts()));
        Assert.Empty(without.FindAll(".pmb-poolsplit-seg"));

        var with = RenderComponent<PumbilityHero>(p => p
            .Add(x => x.Page, page).Add(x => x.Charts, page.Charts())
            .Add(x => x.Pools, new[]
            {
                new PumbilityHero.PoolOption(null, "All", 18041),
                new PumbilityHero.PoolOption(ChartType.Single, "Singles", 17969),
                new PumbilityHero.PoolOption(ChartType.Double, "Doubles", 17864)
            }));
        Assert.Equal(3, with.FindAll(".pmb-poolsplit-seg").Count);
    }

    // ------------------------------------------------------------------ breakdown

    [Fact]
    public void TheBreakdownDrawsThePlateSegmentTrueToScale()
    {
        // A hairline, because it is a hairline. Widening it to be visible would argue the
        // opposite of what the section is for.
        var cut = RenderComponent<PumbilityBreakdown>(p => p
            .Add(x => x.Breakdown, new PoolBreakdown(12442, 5524, 75, 174))
            .Add(x => x.PoolCount, 50));

        var segments = cut.FindAll(".pmb-wpc-seg");
        Assert.Equal(3, segments.Count);
        var plate = double.Parse(segments[2].GetAttribute("style")!.Split(':')[1]);
        var level = double.Parse(segments[0].GetAttribute("style")!.Split(':')[1]);
        Assert.True(plate * 50 < level, "the plate segment was drawn larger than its own share");
    }

    [Fact]
    public void APlatelessMixSaysSoRatherThanDrawingAnEmptyRail()
    {
        // Phoenix 1: every plate modifier is exactly 1.0, so there is no span to magnify and a
        // rail pinned at zero would read as a thing you could move.
        var cut = RenderComponent<PumbilityBreakdown>(p => p
            .Add(x => x.Breakdown, new PoolBreakdown(58242, 6225, 0, 0))
            .Add(x => x.PoolCount, 50));

        Assert.Empty(cut.FindAll(".pmb-wpc-rail"));
        Assert.Contains("nothing", cut.Find(".pmb-wpc-say").TextContent);
    }

    [Fact]
    public void AnEmptyPoolRendersNoBandAtAll()
    {
        var cut = RenderComponent<PumbilityBreakdown>(p => p
            .Add(x => x.Breakdown, new PoolBreakdown(0, 0, 0, 0))
            .Add(x => x.PoolCount, 0));

        Assert.Empty(cut.FindAll(".pmb-wpc"));
    }

    // ------------------------------------------------------------------ curve

    [Fact]
    public void TheCurveDrawsThePoolAndGhostsTheWaitingRoom()
    {
        var page = Page(poolSize: 50, waiting: 6);

        var cut = RenderComponent<PoolCurve>(p => p
            .Add(x => x.Page, page)
            .Add(x => x.Charts, page.Charts()));

        Assert.Equal(56, cut.FindAll(".pmb-cbar").Count);
        Assert.Equal(6, cut.FindAll(".pmb-cbar.ghost").Count);
        Assert.Single(cut.FindAll(".pmb-curve-line"));
    }

    [Fact]
    public void TheCurveSaysWhatItsShapeMeans()
    {
        var page = Page(poolSize: 50, waiting: 6);

        var cut = RenderComponent<PoolCurve>(p => p
            .Add(x => x.Page, page)
            .Add(x => x.Charts, page.Charts()));

        Assert.False(string.IsNullOrWhiteSpace(cut.Find(".pmb-curve-read").TextContent));
    }

    // ------------------------------------------------------------------ carryover

    [Fact]
    public void TheCarryoverNamesTheFlipOnlyWhenThePoolActuallyChangedHands()
    {
        var flipped = new Phoenix2CarryoverRecord(18041, 358, 15, 49, Array.Empty<ProjectedTitle>(),
            32, 18, 4, 46, Array.Empty<CarryoverEntry>(), Array.Empty<CarryoverEntry>());
        var steady = flipped with { SinglesInPool = 4, DoublesInPool = 46 };

        var withFlip = RenderComponent<CarryoverPanel>(p => p
            .Add(x => x.Carryover, flipped).Add(x => x.Charts, new Dictionary<Guid, Chart>()));
        Assert.Single(withFlip.FindAll(".pmb-flip-say"));

        var withoutFlip = RenderComponent<CarryoverPanel>(p => p
            .Add(x => x.Carryover, steady).Add(x => x.Charts, new Dictionary<Guid, Chart>()));
        Assert.Empty(withoutFlip.FindAll(".pmb-flip-say"));
    }

    [Fact]
    public void TheCarryoverChipsSayWouldRatherThanClaimingATitleHeld()
    {
        // These and the rails on PUMBILITY Breakdown say opposite things about the same three ladders at
        // a launch, so the conditional wording and the pool value beside each chip are what
        // keeps them apart (§8.2).
        var carry = new Phoenix2CarryoverRecord(18041, 358, 15, 49, new[]
            {
                new ProjectedTitle(PumbilityPool.Total, 18041, "[P.B] RED BERYL", 19000),
                new ProjectedTitle(PumbilityPool.Singles, 17969, "[S] EXPERT LV.3", 18100),
                new ProjectedTitle(PumbilityPool.Doubles, 17864, null, 5000)
            },
            32, 18, 4, 46, Array.Empty<CarryoverEntry>(), Array.Empty<CarryoverEntry>());

        var cut = RenderComponent<CarryoverPanel>(p => p
            .Add(x => x.Carryover, carry).Add(x => x.Charts, new Dictionary<Guid, Chart>()));

        // A ladder the record does not reach yet gets no chip rather than an empty one.
        Assert.Equal(2, cut.FindAll(".pmb-chip").Count);
        Assert.Contains("would land you", cut.Find(".pmb-lands-lbl").TextContent);
        Assert.Contains("18,041", cut.Find(".pmb-chip-m").TextContent);
    }

    [Fact]
    public void TheUnplayableChartTileIsGone()
    {
        // Cut for saying something nobody can act on. Nothing renders it any more, and nothing
        // should start.
        var carry = new Phoenix2CarryoverRecord(18041, 358, 15, 49, Array.Empty<ProjectedTitle>(),
            32, 18, 4, 46, Array.Empty<CarryoverEntry>(), Array.Empty<CarryoverEntry>());

        var cut = RenderComponent<CarryoverPanel>(p => p
            .Add(x => x.Carryover, carry).Add(x => x.Charts, new Dictionary<Guid, Chart>()));

        Assert.Empty(cut.FindAll(".pmb-fact-warn"));
    }

    // ------------------------------------------------------------------ helpers

    private static Chart NewChart(ChartType type, int level) =>
        new(Guid.NewGuid(), MixEnum.Phoenix,
            new Song($"Song {Guid.NewGuid():N}"[..12], SongType.Arcade,
                new Uri("https://piu.test/i.png"), TimeSpan.FromMinutes(2), "Artist", 180),
            type, level, MixEnum.Phoenix, null, null);

    private static PageFixture Page(int poolSize, int waiting = 0, int targets = 0) =>
        new(poolSize, waiting, targets);

    private sealed record PageFixture
    {
        private readonly Dictionary<Guid, Chart> _charts = new();

        public PageFixture(int poolSize, int waiting, int targets)
        {
            var pool = new List<PoolEntry>();
            for (var i = 0; i < poolSize; i++)
            {
                var chart = NewChart(i % 2 == 0 ? ChartType.Single : ChartType.Double, 20);
                _charts[chart.Id] = chart;
                pool.Add(new PoolEntry(i + 1, chart.Id, 995_000 - i * 500, PhoenixPlate.MarvelousGame,
                    false, DateTimeOffset.UtcNow.AddDays(-30), 1_500 - i * 5));
            }

            var room = new List<PoolEntry>();
            for (var i = 0; i < waiting; i++)
            {
                var chart = NewChart(ChartType.Double, 20);
                _charts[chart.Id] = chart;
                room.Add(new PoolEntry(poolSize + i + 1, chart.Id, 940_000, PhoenixPlate.TalentedGame,
                    false, DateTimeOffset.UtcNow.AddDays(-60), 1_500 - poolSize * 5 - i));
            }

            var list = new List<PumbilityTarget>();
            for (var i = 0; i < targets; i++)
            {
                var chart = NewChart(ChartType.Single, 21);
                _charts[chart.Id] = chart;
                // Both descend and both stay in range at any count — the old formula ran the
                // score past 1,000,000 at the 31st target and the gain negative at the 21st,
                // which capped every fixture at a size too small to page.
                list.Add(new PumbilityTarget(chart.Id, Math.Max(900_000, 999_000 - i * 1_000),
                    Math.Max(1, 400 - i * 5),
                    i % 2 == 0 ? null : 930_000, false, null));
            }

            Pool = pool;
            WaitingRoom = room;
            Targets = list;
            Total = pool.Sum(p => p.Value);
            Bar = pool.Count >= 50 ? pool[^1].Value : null;
            BarChartId = pool.Count >= 50 ? pool[^1].ChartId : null;
        }

        public IReadOnlyList<PoolEntry> Pool { get; }
        public IReadOnlyList<PoolEntry> WaitingRoom { get; }
        public IReadOnlyList<PumbilityTarget> Targets { get; }
        public double Total { get; }
        public double? Bar { get; }
        public Guid? BarChartId { get; }

        public IReadOnlyDictionary<Guid, Chart> Charts() => _charts;

        public static implicit operator PumbilityPageRecord(PageFixture f) =>
            new(MixEnum.Phoenix, null, f.Total, f.Bar, f.BarChartId, f.Pool, f.WaitingRoom, f.Targets);
    }
}
