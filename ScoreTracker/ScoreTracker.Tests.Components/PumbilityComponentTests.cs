using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Bunit;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Services;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services.Contracts;
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


    // ------------------------------------------------------------------ you against your peers (D58)

    private static readonly Guid Me = Guid.NewGuid();

    private void SignIn()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User)
            .Returns(new User(Me, "Me", true, null, new Uri("https://piu.test/me.png"), null));
    }

    private const string DiamondLevel = "[P.B] DIAMOND LV.2";

    private static PumbilityCohortRecord Cohort(PoolTypeSplit? split, string band = DiamondLevel,
        int holders = 64, int boardHolders = 0, DateTimeOffset? asOf = null) => new(
        Name.From(band), holders, boardHolders,
        new PeerLevelSpread(holders, boardHolders, new[]
        {
            new LevelSpreadColumn(20, ChartType.Single, new Dictionary<int, int> { [25] = holders },
                25, 25, 25, 25, 25, holders, 25, 0, holders)
        }),
        split, asOf);

    private IRenderedComponent<PumbilityBreakdown> Card(PumbilityPageRecord page,
        IReadOnlyDictionary<Guid, Chart> charts) =>
        RenderComponent<PumbilityBreakdown>(p => p
            .Add(x => x.Breakdown, new PoolBreakdown(12442, 5524, 75, 174)).Add(x => x.PoolCount, 50)
            .Add(x => x.Page, page).Add(x => x.Charts, charts));

    [Fact]
    public void TheCardSplitsYourFiftyByTypeAndSetsTheCohortsAverageBeneath()
    {
        SignIn();
        var page = Page(poolSize: 50);
        Mediator.Setup(m => m.Send(It.Is<GetPumbilityTitleCohortQuery>(q => q.Pool == PumbilityPool.Total),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cohort(new PoolTypeSplit(64, 34.4, 15.6, 12_085.84, 5_508.22)));

        var cut = RenderComponent<PumbilityBreakdown>(p => p
            .Add(x => x.Breakdown, new PoolBreakdown(12442, 5524, 75, 174)).Add(x => x.PoolCount, 50)
            .Add(x => x.Page, (PumbilityPageRecord)page).Add(x => x.Charts, page.Charts()));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=wpc-levels]")));
        var bars = cut.FindAll("[data-testid=wpc-types] .pmb-flip-bar");
        Assert.Equal(2, bars.Count);
        // Your fifty alternates the types, twenty-five of each; each segment is its worth with
        // the count, doubles first, as the Phoenix 1 card draws it.
        var mine = bars[0].QuerySelectorAll(".pmb-flip-seg").ToArray();
        Assert.Equal(2, mine.Length);
        Assert.All(mine, s => Assert.EndsWith("(25)", s.TextContent.Trim()));
        Assert.Contains("d", mine[0].ClassName.Split(' '));
        var theirs = bars[1].QuerySelectorAll(".pmb-flip-seg").Select(s => s.TextContent.Trim()).ToArray();
        // Whole numbers on the bar (owner, 2026-09-05): the segment is too narrow for decimals.
        Assert.Equal(new[] { "5,508 (16)", "12,086 (34)" }, theirs);
        // The row is labelled by the band rather than by a word for a group (D68).
        Assert.Equal(DiamondLevel, cut.FindAll("[data-testid=wpc-types] .pmb-splitrow-lbl")[1].TextContent.Trim());
        Assert.Contains("the average top 50 of the 64 of them", cut.Find("[data-testid=wpc-types]").TextContent);
        // Sized by value: the singles segment is the wider one on their bar.
        var flex = bars[1].QuerySelectorAll(".pmb-flip-seg")
            .Select(s => double.Parse(s.GetAttribute("style")!.Split(':')[1], CultureInfo.InvariantCulture)).ToArray();
        Assert.True(flex[1] > flex[0]);
        Assert.Single(cut.FindAll("[data-testid=wpc-levels] .pmb-spread-tile"));
    }

    [Fact]
    public void TheBandLineNamesTheTitleAndCountsWhoHoldsIt()
    {
        SignIn();
        var page = Page(poolSize: 50);
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityTitleCohortQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cohort(null, holders: 530, boardHolders: 435,
                asOf: new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero)));

        var cut = Card((PumbilityPageRecord)page, page.Charts());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=cohort-count]")));
        var line = cut.Find("[data-testid=cohort-count]").TextContent;
        Assert.Contains(DiamondLevel, line);
        Assert.Contains("530 players hold it", line);
        // A surface that counts a board player says how old that side of it is (peers-abstraction D37).
        Assert.Contains("435 from the official board, as of 6 Sep", line);
    }

    [Fact]
    public async Task PickingABandReadsThatOneAndNeverWritesItDown()
    {
        SignIn();
        var fixture = Page(poolSize: 50);
        var page = ((PumbilityPageRecord)fixture) with { Mix = MixEnum.Phoenix2 };
        var asked = new List<Name?>();
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityTitleCohortQuery>(), It.IsAny<CancellationToken>()))
            .Callback((object q, CancellationToken _) => asked.Add(((GetPumbilityTitleCohortQuery)q).Band))
            .ReturnsAsync(Cohort(null));

        var cut = Card(page, fixture.Charts());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=cohort-select]")));
        await cut.Find("[data-testid=cohort-select]").ChangeAsync(new ChangeEventArgs { Value = "[P.B] BRONZE" });

        Assert.Equal(new Name?[] { null, Name.From("[P.B] BRONZE") }, asked);
        // The choice lasts the visit and no longer (owner, 2026-09-12). Nothing is stored, so
        // nothing has to be read back, validated against the ladder in scope, or invalidated.
        UiSettings.Verify(s => s.SetSetting(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        UiSettings.Verify(s => s.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(),
            It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public void TheCardOpensOnYourOwnBandEveryVisit()
    {
        // It describes where you stand, so it opens on where you stand — the query's own answer,
        // gem-flipped when the level is too thin to read (D68). A second visit is a second null.
        SignIn();
        var fixture = Page(poolSize: 50);
        var page = ((PumbilityPageRecord)fixture) with { Mix = MixEnum.Phoenix2 };
        var asked = new List<Name?>();
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityTitleCohortQuery>(), It.IsAny<CancellationToken>()))
            .Callback((object q, CancellationToken _) => asked.Add(((GetPumbilityTitleCohortQuery)q).Band))
            .ReturnsAsync(Cohort(null));

        Card(page, fixture.Charts()).WaitForAssertion(() => Assert.Single(asked));
        Card(page, fixture.Charts()).WaitForAssertion(() => Assert.Equal(2, asked.Count));

        Assert.Equal(new Name?[] { null, null }, asked);
    }

    [Fact]
    public async Task ABandFromAnotherLadderReadsAsNoChoiceAtAll()
    {
        // A [P.B] gem picked on the merged pool is not a rung of the singles ladder, so switching
        // pools drops it rather than sending it to a query that could only answer nothing for it.
        SignIn();
        var fixture = Page(poolSize: 50);
        var charts = fixture.Charts();
        var page = ((PumbilityPageRecord)fixture) with { Mix = MixEnum.Phoenix2 };
        var asked = new List<Name?>();
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityTitleCohortQuery>(), It.IsAny<CancellationToken>()))
            .Callback((object q, CancellationToken _) => asked.Add(((GetPumbilityTitleCohortQuery)q).Band))
            .ReturnsAsync(Cohort(null));

        var cut = Card(page, charts);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=cohort-select]")));
        await cut.Find("[data-testid=cohort-select]").ChangeAsync(new ChangeEventArgs { Value = "[P.B] DIAMOND" });
        cut.SetParametersAndRender(p => p.Add(x => x.Page, page with { Pool_ = ChartType.Single }));

        cut.WaitForAssertion(() => Assert.Equal(3, asked.Count));
        Assert.Equal(new Name?[] { null, Name.From("[P.B] DIAMOND"), null }, asked);
    }

    [Fact]
    public void ATypePoolKeepsTheLevelsAndDropsTheSplit()
    {
        // A singles or doubles pool is one type by definition: nothing to split, still a level to sit at.
        SignIn();
        var page = Page(poolSize: 50);
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityTitleCohortQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cohort(null));

        var cut = RenderComponent<PumbilityBreakdown>(p => p
            .Add(x => x.Breakdown, new PoolBreakdown(12442, 5524, 75, 174)).Add(x => x.PoolCount, 50)
            .Add(x => x.Page, ((PumbilityPageRecord)page) with { Pool_ = ChartType.Single }).Add(x => x.Charts, page.Charts()));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=wpc-levels]")));
        Assert.Empty(cut.FindAll("[data-testid=wpc-types]"));
    }

    [Fact]
    public void YourBarStandsAloneWhileNobodyHoldsAFullFifty()
    {
        SignIn();
        var page = Page(poolSize: 50);
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityTitleCohortQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cohort(null));

        var cut = RenderComponent<PumbilityBreakdown>(p => p
            .Add(x => x.Breakdown, new PoolBreakdown(12442, 5524, 75, 174)).Add(x => x.PoolCount, 50)
            .Add(x => x.Page, (PumbilityPageRecord)page).Add(x => x.Charts, page.Charts()));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=wpc-levels]")));
        Assert.Single(cut.FindAll("[data-testid=wpc-types] .pmb-flip-bar"));
        Assert.DoesNotContain("the average top 50", cut.Find("[data-testid=wpc-types]").TextContent);
    }

    [Fact]
    public void ACardWithoutAFrameRecordReadsNothingAndKeepsToItsThreeParts()
    {
        var cut = RenderComponent<PumbilityBreakdown>(p => p
            .Add(x => x.Breakdown, new PoolBreakdown(12442, 5524, 75, 174)).Add(x => x.PoolCount, 50));

        Assert.Empty(cut.FindAll(".pmb-wpc-sub"));
        Mediator.Verify(m => m.Send(It.IsAny<GetPumbilityTitleCohortQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    // ------------------------------------------------------------------ helpers

    private static Chart NewChart(ChartType type, int level) =>
        new(Guid.NewGuid(), MixEnum.Phoenix,
            new Song($"Song {Guid.NewGuid():N}"[..12], SongType.Arcade,
                new Uri("https://piu.test/i.png"), TimeSpan.FromMinutes(2), "Artist", 180),
            type, level, MixEnum.Phoenix, null, null);

    private static ArchetypeSpread Archetypes(int[] counts, double? mine, int boardHolders = 0,
        MixEnum mix = MixEnum.Phoenix2)
    {
        var byType = Enum.GetValues<RecapPlayerType>()
            .Select((type, i) => (type, count: counts[i])).ToDictionary(x => x.type, x => x.count);
        var cohort = new CohortArchetypeSpread(counts.Sum(), byType);
        return ArchetypeSpread.Of(cohort, boardHolders,
            mine is { } average
                ? Enumerable.Repeat((int)average, RecapPlayerTypeCalculator.MinimumScores).ToArray()
                : Array.Empty<int>(), mix);
    }

    [Fact]
    public void TheArchetypeSpectrumSizesEachBandByTheShareOfTheCohortStandingInIt()
    {
        SignIn();
        var page = Page(poolSize: 50);
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityTitleCohortQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cohort(null) with { Archetypes = Archetypes(new[] { 10, 20, 30, 20, 20 }, 977_000) });

        var cut = Card(page, page.Charts());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=archetype-spectrum]")));
        var bands = cut.FindAll("[data-testid=archetype-spectrum] .pmb-arch-band");
        Assert.Equal(5, bands.Count);
        Assert.Contains("width:10%", bands[0].GetAttribute("style"));
        Assert.Contains("width:30%", bands[2].GetAttribute("style"));
        // The viewer's own band is the outlined one, and it is the one their average falls in.
        Assert.Equal("3", Assert.Single(bands, b => b.ClassList.Contains("is-mine")).GetAttribute("data-k"));
        // Every band's count prints beneath it, the empty ones included.
        Assert.Equal(new[] { "10", "20", "30", "20", "20" },
            cut.FindAll("[data-testid=archetype-spectrum] .pmb-arch-tick")
                .Select(t => t.TextContent.Trim()).ToArray());
    }

    [Fact]
    public void TheMarkerSitsInsideTheBandTheLineNames()
    {
        SignIn();
        var page = Page(poolSize: 50);
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityTitleCohortQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cohort(null) with { Archetypes = Archetypes(new[] { 10, 20, 30, 20, 20 }, 982_000) });

        var cut = Card(page, page.Charts());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=archetype-spectrum]")));
        var marker = cut.Find("[data-testid=archetype-spectrum] .pmb-arch-you");
        var at = double.Parse(marker.GetAttribute("style")!.Replace("left:", "").Replace("%", ""),
            CultureInfo.InvariantCulture);
        // Competitive spans 60% to 80% of the strip, and the marker has to land inside it or the
        // drawing outlines one archetype while pointing at another.
        Assert.InRange(at, 60, 80);
        Assert.Equal("4", Assert.Single(cut.FindAll("[data-testid=archetype-spectrum] .pmb-arch-band"),
            b => b.ClassList.Contains("is-mine")).GetAttribute("data-k"));
    }

    [Fact]
    public void TheLineNamesTheBandAndTheShareHoldingItAndNeverAStanding()
    {
        SignIn();
        var page = Page(poolSize: 50);
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityTitleCohortQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cohort(null) with { Archetypes = Archetypes(new[] { 10, 20, 30, 20, 20 }, 977_000) });

        var cut = Card(page, page.Charts());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=archetype-say]")));
        var said = cut.Find("[data-testid=archetype-say]").TextContent;
        Assert.Contains("977,000", said);
        Assert.Contains("Balanced Player", said);
        Assert.Contains("30%", said);
        Assert.Contains(DiamondLevel, said);
        // The archetypes are lateral (D69): nothing on this section may rank them.
        foreach (var ranking in new[] { "ahead of", "above", "higher", "top " })
            Assert.DoesNotContain(ranking, said, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TooShortAFiftyDrawsTheCohortAndSaysSoInsteadOfPlacingYou()
    {
        SignIn();
        var page = Page(poolSize: 50);
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityTitleCohortQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cohort(null) with { Archetypes = Archetypes(new[] { 10, 20, 30, 20, 20 }, null) });

        var cut = Card(page, page.Charts());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=archetype-spectrum]")));
        Assert.Equal(5, cut.FindAll("[data-testid=archetype-spectrum] .pmb-arch-band").Count);
        Assert.Empty(cut.FindAll("[data-testid=archetype-spectrum] .pmb-arch-you"));
        Assert.DoesNotContain(cut.FindAll("[data-testid=archetype-spectrum] .pmb-arch-band"),
            b => b.ClassList.Contains("is-mine"));
    }

    [Fact]
    public void ABandTooNarrowForItsLabelDropsTheLabelRatherThanClippingIt()
    {
        SignIn();
        var page = Page(poolSize: 50);
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityTitleCohortQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cohort(null) with { Archetypes = Archetypes(new[] { 1, 0, 60, 20, 19 }, 977_000) });

        var cut = Card(page, page.Charts());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=archetype-spectrum]")));
        var bands = cut.FindAll("[data-testid=archetype-spectrum] .pmb-arch-band");
        Assert.Empty(bands[0].QuerySelectorAll("b"));
        Assert.Empty(bands[1].QuerySelectorAll("b"));
        Assert.Equal("BP", bands[2].QuerySelectorAll("b").Single().TextContent);
    }

    [Fact]
    public void ATypedPoolDrawsNoArchetypeSection()
    {
        SignIn();
        var page = Page(poolSize: 50);
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityTitleCohortQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cohort(null));

        var cut = Card(((PumbilityPageRecord)page) with { Pool_ = ChartType.Single }, page.Charts());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=wpc-levels]")));
        Assert.Empty(cut.FindAll("[data-testid=wpc-archetypes]"));
    }

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
