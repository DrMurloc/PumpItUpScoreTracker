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
        Assert.Equal(4, comfortable.FindAll(".tier-chart-card").Count);
        Assert.Empty(comfortable.FindAll(".tier-chart-card-compact"));

        var compact = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, page.Targets).Add(x => x.Charts, charts)
            .Add(x => x.Density, UiDensity.Compact));
        Assert.Equal(4, compact.FindAll(".tier-chart-card-compact").Count);

        var table = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, page.Targets).Add(x => x.Charts, charts)
            .Add(x => x.Density, UiDensity.Table));
        Assert.Equal(4, table.FindAll("tbody tr").Count);
    }

    [Fact]
    public void TheCompactBorderSaysWhichOfTheThreeKindsOfRowThisIs()
    {
        // A Compact card prints one number and no words, so the BORDER carries the kind — in
        // the tier list's own language, reused rather than reinvented so the two pages cannot
        // drift apart.
        var cut = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, ThreeKinds(out var charts)).Add(x => x.Charts, charts)
            .Add(x => x.Density, UiDensity.Compact));

        // Scoped to the grid: the legend wears the same classes on purpose, so a swatch is a
        // second match for every kind the grid contains.
        Assert.Single(cut.FindAll(".tier-card-grid .tier-chart-card-pass"));
        Assert.Single(cut.FindAll(".tier-card-grid .tier-chart-card-other-mix"));
        // The third kind is the absence of a border, so it is counted by what it is NOT.
        Assert.Equal(3, cut.FindAll(".tier-card-grid .tier-chart-card").Count);
    }

    [Fact]
    public void TheGainBadgeIsOneTreatmentAndOnlySaysHowMuch()
    {
        var cut = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, ThreeKinds(out var charts)).Add(x => x.Charts, charts)
            .Add(x => x.Density, UiDensity.Compact));

        // Scoped to the end corner: the jacket carries two badges now, and they share the
        // treatment on purpose — .tier-chart-card-corner alone would count both.
        var badges = cut.FindAll(".tier-card-grid .tier-chart-card-compact-grade");
        Assert.Equal(3, badges.Count);
        Assert.All(badges, b => Assert.Contains("pmb-corner-gain", b.ClassName));
        Assert.Contains("+400", badges.Select(b => b.TextContent.Trim()).ToArray());
    }

    [Fact]
    public void ComfortableNeverPaintsAStateBorder()
    {
        // Comfortable says the kind in words on every card's why-line, and the To-Do bookmark
        // would otherwise paint a fourth ring competing with the three that mean something.
        var targets = ThreeKinds(out var charts);
        var cut = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, targets).Add(x => x.Charts, charts)
            .Add(x => x.Density, UiDensity.Comfortable)
            .Add(x => x.ShowToDo, true)
            .Add(x => x.ToDos, (ISet<Guid>)targets.Select(t => t.ChartId).ToHashSet()));

        Assert.Empty(cut.FindAll(".tier-chart-card-pass"));
        Assert.Empty(cut.FindAll(".tier-chart-card-other-mix"));
        Assert.Empty(cut.FindAll(".tier-chart-card-todo"));
    }

    [Fact]
    public void CompactPrintsTheGradeTheProjectionLandsOnOppositeTheGain()
    {
        // Two badges on the jacket's bottom edge, one treatment: how much on the right, what
        // you would come away with on the left. The words for it ride the tooltip, which
        // renders into the popover provider and so is not assertable from here.
        var cut = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, ThreeKinds(out var charts)).Add(x => x.Charts, charts)
            .Add(x => x.Density, UiDensity.Compact));

        var grades = cut.FindAll(".tier-card-grid .tier-chart-card-corner-start");
        Assert.Equal(3, grades.Count);
        Assert.All(grades, g => Assert.Contains("pmb-corner-gain", g.ClassName));
        // The icon, not a specific letter: which grade a score earns is the scoring
        // configuration's business and pinning one here would test that instead of this.
        Assert.All(grades, g => Assert.Contains("/letters/", g.InnerHtml));
    }

    [Fact]
    public void ABrokenRunGetsItsOwnBorderRatherThanReadingAsAnUpscore()
    {
        // A stage break is worth nothing, so the arithmetic prices the chart from the bar as
        // though it had never been played. A solid "upscore" border would then be describing a
        // score the page has just decided the player does not hold — and no border at all
        // would claim they have never touched it.
        var broke = NewChart(ChartType.Single, 22);
        var targets = new[] { new PumbilityTarget(broke.Id, 980_000, 300, 640_000, true, null, null) };

        var cut = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, targets)
            .Add(x => x.Charts, new Dictionary<Guid, Chart> { [broke.Id] = broke })
            .Add(x => x.Density, UiDensity.Compact));

        Assert.Single(cut.FindAll(".tier-card-grid .tier-chart-card-broken"));
        Assert.Empty(cut.FindAll(".tier-card-grid .tier-chart-card-pass"));
        Assert.Contains("Played, not passed", cut.Find(".pmb-legend").TextContent);
    }

    /// <summary>One of each kind: carried from another mix, an upscore, and never played.</summary>
    private static PumbilityTarget[] ThreeKinds(out Dictionary<Guid, Chart> charts)
    {
        var carried = NewChart(ChartType.Single, 22);
        var upscore = NewChart(ChartType.Single, 22);
        var unplayed = NewChart(ChartType.Single, 22);
        charts = new Dictionary<Guid, Chart>
            { [carried.Id] = carried, [upscore.Id] = upscore, [unplayed.Id] = unplayed };
        return new[]
        {
            new PumbilityTarget(carried.Id, 985_000, 400, null, false, null, null, TargetSource.Phoenix1),
            new PumbilityTarget(upscore.Id, 980_000, 200, 930_000, false, null, null),
            new PumbilityTarget(unplayed.Id, 970_000, 300, null, false, null, null)
        };
    }

    [Fact]
    public void CompactPrintsTheWordsForEveryColourItIsUsing()
    {
        // Rule 8: Compact has no room for a word on the card, so the legend carries it —
        // and only for kinds the grid actually contains.
        var carried = NewChart(ChartType.Single, 22);
        var unplayed = NewChart(ChartType.Single, 22);
        var targets = new[]
        {
            new PumbilityTarget(carried.Id, 985_000, 400, null, false, null, null, TargetSource.Phoenix1),
            new PumbilityTarget(unplayed.Id, 970_000, 300, null, false, null, null)
        };

        var cut = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, targets)
            .Add(x => x.Charts, new Dictionary<Guid, Chart> { [carried.Id] = carried, [unplayed.Id] = unplayed })
            .Add(x => x.Density, UiDensity.Compact));

        var legend = cut.Find(".pmb-legend").TextContent;
        Assert.Contains("Phoenix 1", legend);
        Assert.Contains("Not yet played", legend);
        // No row is an upscore, so a swatch for one would be a state you failed to find.
        Assert.DoesNotContain("Upscore", legend);
    }

    [Fact]
    public void TheTableStillNamesItsSourceBecauseAColumnCanHoldAWord()
    {
        var carried = NewChart(ChartType.Single, 22);
        var guessed = NewChart(ChartType.Single, 22);
        var targets = new[]
        {
            new PumbilityTarget(carried.Id, 985_000, 400, null, false, null, null, TargetSource.Phoenix1),
            new PumbilityTarget(guessed.Id, 960_000, 300, null, false, null,
                new ProjectionEvidence(20, 15, 9_000))
        };

        var cut = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, targets)
            .Add(x => x.Charts, new Dictionary<Guid, Chart> { [carried.Id] = carried, [guessed.Id] = guessed })
            .Add(x => x.Density, UiDensity.Table));

        Assert.Equal(2, cut.FindAll(".pmb-src").Count);
        Assert.Single(cut.FindAll(".pmb-src-carry"));
        Assert.Contains("Phoenix 1", cut.Find(".pmb-src-carry").TextContent);
    }

    [Fact]
    public void ThereIsNoSourceLabelWhenEveryRowIsTheSameKind()
    {
        // Phoenix 1 has no carryover, so a column reading "Players" down every row is noise.
        var chart = NewChart(ChartType.Single, 20);
        var targets = new[]
        {
            new PumbilityTarget(chart.Id, 970_000, 300, null, false, null, new ProjectionEvidence(20, 15, 9_000))
        };

        var cut = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, targets)
            .Add(x => x.Charts, new Dictionary<Guid, Chart> { [chart.Id] = chart })
            .Add(x => x.Density, UiDensity.Table));

        Assert.Empty(cut.FindAll(".pmb-src"));
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
    public void AVanishedChartIsReportedAsAFactAndNamed()
    {
        var lost = NewChart(ChartType.Single, 22);
        var carry = new Phoenix2CarryoverRecord(18041, 358, 15, 49, new[] { lost.Id },
            32, 18, 4, 46, Array.Empty<CarryoverEntry>(), Array.Empty<CarryoverEntry>());

        var cut = RenderComponent<CarryoverPanel>(p => p
            .Add(x => x.Carryover, carry)
            .Add(x => x.Charts, new Dictionary<Guid, Chart> { [lost.Id] = lost }));

        var fact = cut.Find(".pmb-fact-warn");
        Assert.Contains("No Phoenix 2 chart", fact.TextContent);
        Assert.Contains(lost.Song.Name.ToString(), fact.TextContent);
    }

    // ------------------------------------------------------------------ helpers

    [Fact]
    public void ALongListPagesRatherThanRunningOffTheScreen()
    {
        var page = Page(poolSize: 50, targets: 70);
        var charts = page.Charts();

        var cut = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, page.Targets).Add(x => x.Charts, charts)
            .Add(x => x.Density, UiDensity.Comfortable));

        // Comfortable pages at 24; the pager is what makes the other 46 reachable.
        Assert.Equal(24, cut.FindAll(".tier-chart-card").Count);
        Assert.NotEmpty(cut.FindAll(".srp-pager"));
    }

    [Fact]
    public void AListThatFitsOnOnePageShowsNoPager()
    {
        var page = Page(poolSize: 50, targets: 5);

        var cut = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, page.Targets).Add(x => x.Charts, page.Charts())
            .Add(x => x.Density, UiDensity.Comfortable));

        Assert.Empty(cut.FindAll(".srp-pager"));
    }

    [Fact]
    public void AShorterListCannotStrandTheReaderOnAPageThatIsGone()
    {
        // Page 3 exists at 70 targets. Switching pool re-runs the query and can come back with
        // far fewer, and a stale page number renders an empty slice off the end rather than an
        // empty state.
        var many = Page(poolSize: 50, targets: 70);
        var cut = RenderComponent<TargetList>(p => p
            .Add(x => x.Targets, many.Targets).Add(x => x.Charts, many.Charts())
            .Add(x => x.Density, UiDensity.Comfortable));
        // Driven through the pager's callback, not a DOM click: MudPagination's items carry
        // no onclick bUnit can dispatch.
        var pager = cut.FindComponent<MudBlazor.MudPagination>();
        cut.InvokeAsync(() => pager.Instance.SelectedChanged.InvokeAsync(3)).Wait();

        var few = Page(poolSize: 50, targets: 3);
        cut.SetParametersAndRender(p => p
            .Add(x => x.Targets, few.Targets).Add(x => x.Charts, few.Charts()));

        Assert.Equal(3, cut.FindAll(".tier-chart-card").Count);
    }

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
                // Both descend and both stay in range at any count — the old formula ran the
                // score past 1,000,000 at the 31st target and the gain negative at the 21st,
                // which capped every fixture at a size too small to page.
                list.Add(new PumbilityTarget(chart.Id, Math.Max(900_000, 999_000 - i * 1_000),
                    Math.Max(1, 400 - i * 5),
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
