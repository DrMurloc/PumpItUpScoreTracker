using Bunit;
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

        Assert.Contains(page.Total.ToString("N0"), cut.Find(".pmb-hero-value").TextContent);
        Assert.Equal(page.Bar!.Value.ToString("N0"), cut.Find(".pmb-barcard-num").TextContent.Trim());
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

    // ------------------------------------------------------------------ targets

    [Fact]
    public void EachDensityRendersItsOwnShape()
    {
        var page = Page(poolSize: 50, targets: 4);
        var charts = page.Charts();

        var comfortable = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, page.Targets).Add(x => x.Charts, charts)
            .Add(x => x.Density, UiDensity.Comfortable));
        Assert.Equal(4, comfortable.FindAll(".pmb-tcard").Count);

        var compact = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, page.Targets).Add(x => x.Charts, charts)
            .Add(x => x.Density, UiDensity.Compact));
        Assert.Equal(4, compact.FindAll(".pmb-sticker").Count);

        var table = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, page.Targets).Add(x => x.Charts, charts)
            .Add(x => x.Density, UiDensity.Table));
        Assert.Equal(4, table.FindAll("tbody tr").Count);
    }

    [Fact]
    public void AnUnplayedChartWearsTheNewRailAndAnUpgradeDoesNot()
    {
        // The whole reason there is no "kind" column: the row already says it.
        var page = Page(poolSize: 50, targets: 0);
        var chart = NewChart(ChartType.Single, 20);
        var targets = new[]
        {
            new PumbilityTarget(chart.Id, 970_000, 300, null, false, null, null),
            new PumbilityTarget(chart.Id, 980_000, 200, 930_000, false, null, null)
        };

        var cut = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, targets)
            .Add(x => x.Charts, new Dictionary<Guid, Chart> { [chart.Id] = chart })
            .Add(x => x.Density, UiDensity.Comfortable));

        Assert.Single(cut.FindAll(".pmb-tcard.is-new"));
        Assert.Equal(2, cut.FindAll(".pmb-tcard").Count);
    }

    [Fact]
    public void TargetsExplainTheirEvidenceAndNeverAttributeSkills()
    {
        var chart = NewChart(ChartType.Single, 20);
        var targets = new[]
        {
            new PumbilityTarget(chart.Id, 970_000, 300, null, false, null,
                new ProjectionEvidence(42, 31.5, 18_000))
        };

        var cut = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, targets)
            .Add(x => x.Charts, new Dictionary<Guid, Chart> { [chart.Id] = chart })
            .Add(x => x.Density, UiDensity.Comfortable));

        Assert.Contains("42", cut.Find(".pmb-tcard-why").TextContent);
    }

    [Fact]
    public void NoTargetsIsAnEmptyStateThatNamesWhatWouldFillIt()
    {
        var cut = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, Array.Empty<PumbilityTarget>())
            .Add(x => x.Charts, new Dictionary<Guid, Chart>()));

        Assert.Contains("play a few more charts", cut.Find(".pmb-empty").TextContent);
    }

    // ------------------------------------------------------------------ board

    [Fact]
    public void ThePoolBoardRulesTheBarAcrossTheList()
    {
        var page = Page(poolSize: 50, waiting: 6);

        var cut = RenderComponent<PoolBoard>(p => p
            .Add(x => x.Page, page)
            .Add(x => x.Charts, page.Charts()));

        Assert.Equal(56, cut.FindAll(".pmb-rankrow").Count);
        Assert.Single(cut.FindAll(".pmb-barrule"));
        Assert.Equal(6, cut.FindAll(".pmb-rankrow.pmb-below").Count);
        Assert.Single(cut.FindAll(".pmb-rankrow.pmb-at-bar"));
    }

    // ------------------------------------------------------------------ carryover

    [Fact]
    public void TheCarryoverNamesTheFlipOnlyWhenThePoolActuallyChangedHands()
    {
        var flipped = new Phoenix2CarryoverRecord(18041, 358, 15, 49, Array.Empty<Guid>(),
            32, 18, 4, 46, Array.Empty<CarryoverEntry>());
        var steady = flipped with { SinglesInPool = 4, DoublesInPool = 46 };

        var withFlip = RenderComponent<CarryoverPanel>(p => p
            .Add(x => x.Carryover, flipped).Add(x => x.Charts, new Dictionary<Guid, Chart>()));
        Assert.Single(withFlip.FindAll(".pmb-flip-say"));

        var withoutFlip = RenderComponent<CarryoverPanel>(p => p
            .Add(x => x.Carryover, steady).Add(x => x.Charts, new Dictionary<Guid, Chart>()));
        Assert.Empty(withoutFlip.FindAll(".pmb-flip-say"));
    }

    [Fact]
    public void AVanishedChartIsReportedAsAFactAndNamed()
    {
        var lost = NewChart(ChartType.Single, 22);
        var carry = new Phoenix2CarryoverRecord(18041, 358, 15, 49, new[] { lost.Id },
            32, 18, 4, 46, Array.Empty<CarryoverEntry>());

        var cut = RenderComponent<CarryoverPanel>(p => p
            .Add(x => x.Carryover, carry)
            .Add(x => x.Charts, new Dictionary<Guid, Chart> { [lost.Id] = lost }));

        var fact = cut.Find(".pmb-fact-warn");
        Assert.Contains("No Phoenix 2 chart", fact.TextContent);
        Assert.Contains(lost.Song.Name.ToString(), fact.TextContent);
    }

    // ------------------------------------------------------------------ helpers

    private static Chart NewChart(ChartType type, int level) =>
        new(Guid.NewGuid(), MixEnum.Phoenix,
            new Song($"Song {Guid.NewGuid():N}"[..12], SongType.Arcade,
                new Uri("https://piu.test/i.png"), TimeSpan.FromMinutes(2), "Artist", 180),
            type, level, MixEnum.Phoenix, null, null, new HashSet<Skill>());

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
                list.Add(new PumbilityTarget(chart.Id, 970_000 + i * 1_000, 400 - i * 20,
                    i % 2 == 0 ? null : 930_000, false, null, new ProjectionEvidence(20 + i, 15.5, 12_000)));
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
        public int Total { get; }
        public int? Bar { get; }
        public Guid? BarChartId { get; }

        public IReadOnlyDictionary<Guid, Chart> Charts() => _charts;

        public static implicit operator PumbilityPageRecord(PageFixture f) =>
            new(MixEnum.Phoenix, null, f.Total, f.Bar, f.BarChartId, f.Pool, f.WaitingRoom, f.Targets);
    }
}
