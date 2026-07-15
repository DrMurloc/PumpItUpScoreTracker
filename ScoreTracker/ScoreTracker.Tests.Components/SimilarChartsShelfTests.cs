using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using MudBlazor.Services;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The shelf's why-chips are the similarity graph's explainability — these pin the
///     design doc's thresholds. Own context (not ComponentTestBase) because the shelf
///     needs its own mediator setups.
/// </summary>
public sealed class SimilarChartsShelfTests : TestContext
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();

    public SimilarChartsShelfTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(o => o.PopoverOptions.CheckForPopoverProvider = false);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        // The default sort is a difficulty lens, so every render reads a tier list. Empty
        // unless a test says otherwise — SetupBlend overrides.
        _mediator.Setup(m => m.Send(It.IsAny<GetBlendedTierListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierListResult(Array.Empty<SongTierListEntry>(), IsProvisionalFallback: false));
        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(_currentUser.Object);
        Services.AddScoped<ChartScoringLevels>();
        var localizer = new Mock<IStringLocalizer<App>>();
        localizer.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        localizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] args) => new LocalizedString(key, string.Format(key, args)));
        Services.AddSingleton(localizer.Object);
    }

    private static ChartSharedBadgeRecord Badge(string badge, double coverage)
    {
        return new ChartSharedBadgeRecord(badge, coverage);
    }

    /// <summary>
    ///     The rendered match chips. A chip is a label and a value in separate elements —
    ///     the gap between them is CSS, not a space — so the text reads "Brackets50%".
    /// </summary>
    private static string[] Chips(IRenderedFragment cut)
    {
        return cut.FindAll(".chart-card-why b").Select(e => e.TextContent.Trim()).ToArray();
    }

    private static ChartSimilarityRecord Edge(double score, params ChartSharedBadgeRecord[] badges)
    {
        return new ChartSimilarityRecord(Guid.NewGuid(), score, SkillScore: score, IntensityScore: score,
            SharedBadges: badges);
    }

    private void SetupEdges(Guid chartId, params ChartSimilarityRecord[] edges)
    {
        SetupEdges(chartId, edges, levels: null);
    }

    private void SetupEdges(Guid chartId, ChartSimilarityRecord[] edges, IReadOnlyDictionary<Guid, int>? levels)
    {
        _mediator.Setup(m => m.Send(It.Is<GetSimilarChartsQuery>(q => q.ChartId == chartId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(edges);
        var charts = new List<Chart> { ChartSlugsTests.BuildChart(chartId, song: "Anchor") };
        foreach (var edge in edges)
            charts.Add(ChartSlugsTests.BuildChart(edge.ChartId, song: $"Song {edge.ChartId:N}",
                level: levels != null && levels.TryGetValue(edge.ChartId, out var l) ? l : 21));
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartStepAnalysesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, ChartStepAnalysisRecord>)
                new Dictionary<Guid, ChartStepAnalysisRecord>());
        _mediator.Setup(m => m.Send(It.IsAny<GetChartVideosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ChartVideoInformation>)Array.Empty<ChartVideoInformation>());
    }

    [Fact]
    public void ChipsNameTheBadgesThePairSharesAtTheirSharedCoverage()
    {
        var anchor = Guid.NewGuid();
        // "Brackets 50%" means BOTH charts are at least half brackets — the shared
        // coverage, not either chart's own. Named badges, never "skills match".
        SetupEdges(anchor, Edge(0.9, Badge("bracket", 0.5), Badge("anchor_run", 0.25)));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.Contains("Brackets50%", Chips(cut));
        Assert.Contains("Anchor runs25%", Chips(cut));
        Assert.DoesNotContain("same skill profile", cut.Markup);
    }

    [Fact]
    public void ATraceOfABadgeIsNotAReason()
    {
        var anchor = Guid.NewGuid();
        // Both charts brush past a drill. That is not something they have in common.
        SetupEdges(anchor, Edge(0.9, Badge("bracket", 0.5), Badge("drill", 0.04)));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.Contains("Brackets50%", Chips(cut));
        Assert.DoesNotContain("Drills", cut.Markup);
    }

    [Fact]
    public void AtMostThreeChipsRender()
    {
        var anchor = Guid.NewGuid();
        SetupEdges(anchor, Edge(0.9, Badge("bracket", 0.9), Badge("anchor_run", 0.8),
            Badge("drill", 0.7), Badge("jack", 0.6)));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.Contains("Brackets90%", Chips(cut));
        Assert.Contains("Anchor runs80%", Chips(cut));
        Assert.Contains("Drills70%", Chips(cut));
        Assert.DoesNotContain("Jacks", cut.Markup);
    }

    [Fact]
    public void AnUnmappedBadgeIsHumanizedRatherThanDumpedRaw()
    {
        // A badge piucenter adds after us degrades to readable jargon, never to a key with
        // underscores in it. The hyphen is the term's own punctuation and stays.
        var anchor = Guid.NewGuid();
        SetupEdges(anchor, Edge(0.9, Badge("spin_move", 0.5), Badge("cross-pad_shuffle", 0.4)));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.Contains("Spin Move50%", Chips(cut));
        Assert.Contains("Cross-pad Shuffle40%", Chips(cut));
    }

    [Fact]
    public void EdgesUnderTheRenderFloorAreNotMatchesButAreStillOffered()
    {
        var anchor = Guid.NewGuid();
        // The graph stores its whole tail floor-free; deciding what counts as a match is
        // the shelf's job. A tail row must not render as a match — but it must not vanish
        // either, because "no significant matches, here's the closest we could find" beats
        // an empty box, and storing the tail is what pays for that.
        SetupEdges(anchor, Edge(0.30, Badge("bracket", 0.5)));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.Contains("No significant matches", cut.Markup);
        Assert.Contains("Brackets50%", Chips(cut));
    }

    [Fact]
    public void MatchesAndNearMissesAreSeparatedRatherThanBlended()
    {
        var anchor = Guid.NewGuid();
        SetupEdges(anchor, Edge(0.80, Badge("bracket", 0.5)), Edge(0.30, Badge("twist_90", 0.4)));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.DoesNotContain("No significant matches", cut.Markup);
        Assert.Contains("Brackets50%", Chips(cut));
        // The near-miss is behind its own disclosure, counted so the offer is honest.
        Assert.Contains("Didn't quite make the cut (1)", cut.Markup);
        Assert.Contains("90° twists40%", Chips(cut));
    }


    /// <summary>
    ///     The panel is plain HTML precisely so it can be driven like this — the old menu
    ///     lived in a MudBlazor popover, which bUnit does not host, so its filters could
    ///     only ever be tested by calling methods behind the UI.
    /// </summary>
    private static IRenderedComponent<SimilarChartsShelf> OpenPanel(IRenderedComponent<SimilarChartsShelf> cut)
    {
        cut.Find(".chart-shelf-addfilter").Click();
        return cut;
    }

    private static void SwitchOn(IRenderedComponent<SimilarChartsShelf> cut, int dimension)
    {
        cut.FindAll(".chart-shelf-ftoggles input")[dimension].Change(true);
    }

    private const int Folder = 0;
    private const int ScoringLevel = 1;
    private const int Bpm = 2;
    private const int Nps = 3;

    [Fact]
    public void NoFilterIsOnUntilOneIsSwitchedOn()
    {
        // An unfiltered shelf already answers "what plays like this". Opening with four
        // ranges pre-applied would put four decisions in front of a reader who wanted none.
        var anchor = Guid.NewGuid();
        SetupEdges(anchor, Edge(0.9, Badge("bracket", 0.5)));
        var cut = OpenPanel(RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor)));

        Assert.All(cut.FindAll(".chart-shelf-ftoggles input"), i => Assert.False(i.HasAttribute("checked")));
        Assert.Empty(cut.FindAll(".range-slider"));
        Assert.Contains("Switch on a filter", cut.Markup);
    }

    [Fact]
    public void SwitchingOnAFilterOpensItOnTheAnchorsOwnNeighbourhood()
    {
        // "Near this chart" is the only range anyone starts from, so the toggle seeds it
        // rather than making the reader find the anchor on a bare track first.
        var anchor = Guid.NewGuid();
        SetupEdges(anchor, Edge(0.9, Badge("bracket", 0.5)));
        var cut = OpenPanel(RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor)));

        SwitchOn(cut, Folder);

        // The anchor is D20 (the test chart's default) and the folder reach is ±2.
        Assert.Contains("D18 – D22", cut.Find(".range-slider-value").TextContent);
    }

    [Fact]
    public void TheCountIsWhatApplyWillActuallyCompare()
    {
        // The count is the honest half of Apply: it is computed from the same numbers the
        // server filters on, so a count that counted differently would be a promise the
        // server breaks. Two D21 edges are in range of a D20 anchor's ±2; the D25 is not,
        // and the anchor never counts itself.
        var anchor = Guid.NewGuid();
        var near = Edge(0.9, Badge("bracket", 0.5));
        var far = Edge(0.8, Badge("bracket", 0.4));
        SetupEdges(anchor, new[] { near, far }, new Dictionary<Guid, int> { [far.ChartId] = 25 });
        var cut = OpenPanel(RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor)));

        SwitchOn(cut, Folder);

        Assert.Contains("Comparing 1 charts", cut.Markup);
    }

    [Fact]
    public void ADimensionTheAnchorHasNoValueForCannotBeSwitchedOn()
    {
        // There is nothing to open a range around. Offering the toggle anyway would seed it
        // from nothing, and the reader would be filtering on a number we do not have.
        var anchor = Guid.NewGuid();
        SetupEdges(anchor, Edge(0.9, Badge("bracket", 0.5)));
        var cut = OpenPanel(RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor)));

        // The test charts carry no BPM and the crawl banked no step analysis, so those two
        // have nothing to say; folder and scoring level always do.
        var toggles = cut.FindAll(".chart-shelf-ftoggles input");
        Assert.False(toggles[Folder].HasAttribute("disabled"));
        Assert.False(toggles[ScoringLevel].HasAttribute("disabled"));
        Assert.True(toggles[Bpm].HasAttribute("disabled"));
        Assert.True(toggles[Nps].HasAttribute("disabled"));
    }

    [Fact]
    public void AnUnmeasuredChartFiltersAtItsListedLevel()
    {
        // GetChartScoringLevelsQuery reports the listed level for a chart nothing has
        // measured, and the shelf agrees with it — otherwise switching on scoring level
        // would silently drop every unmeasured chart out of the count.
        var anchor = Guid.NewGuid();
        SetupEdges(anchor, Edge(0.9, Badge("bracket", 0.5)));
        var cut = OpenPanel(RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor)));

        SwitchOn(cut, ScoringLevel);

        // Nothing is measured here, so the anchor sits at its listed 20.0 and the D21 edge
        // at 21.0 — inside ±2, and counted.
        Assert.Contains("18.0 – 22.0", cut.Find(".range-slider-value").TextContent);
        Assert.Contains("Comparing 1 charts", cut.Markup);
    }

    [Fact]
    public void ApplyNarrowsTheTargetListAndSaysWhatItSearched()
    {
        // The filter reduces what we COMPARE AGAINST and rescores — it never sieves the
        // stored top-20, which are the nearest charts overall and would survive nothing
        // worth filtering by. The reach line is what turns "1 match" from a bug report
        // into a sentence.
        var anchor = Guid.NewGuid();
        SetupEdges(anchor, Edge(0.9, Badge("bracket", 0.5)));
        var filtered = Edge(0.7, Badge("bracket", 0.4));
        GetFilteredSimilarChartsQuery? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetFilteredSimilarChartsQuery>(), It.IsAny<CancellationToken>()))
            .Callback((object q, CancellationToken _) => sent = (GetFilteredSimilarChartsQuery)q)
            .ReturnsAsync(new FilteredSimilarChartsRecord(new[] { filtered }, ChartsCompared: 30));
        var cut = OpenPanel(RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor)));
        SwitchOn(cut, Folder);

        cut.Find(".chart-shelf-fgo").Click();

        Assert.Equal(18, sent!.MinLevel);
        Assert.Equal(22, sent.MaxLevel);
        Assert.Contains("Compared 30 charts", cut.Markup);
        Assert.Single(cut.FindAll(".chart-shelf-fchip"));
    }

    [Fact]
    public void ClearingAFilterGoesBackToThePrecalculatedGraph()
    {
        var anchor = Guid.NewGuid();
        SetupEdges(anchor, Edge(0.9, Badge("bracket", 0.5)));
        _mediator.Setup(m => m.Send(It.IsAny<GetFilteredSimilarChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FilteredSimilarChartsRecord(Array.Empty<ChartSimilarityRecord>(), ChartsCompared: 30));
        var cut = OpenPanel(RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor)));
        SwitchOn(cut, Folder);
        cut.Find(".chart-shelf-fgo").Click();
        Assert.Single(cut.FindAll(".chart-shelf-fchip"));

        cut.Find(".chart-shelf-fclear").Click();

        Assert.Empty(cut.FindAll(".chart-shelf-fchip"));
        Assert.Contains("Brackets50%", Chips(cut));
    }

    [Fact]
    public void TheOppositeIsShownRatherThanFiledUnderDidntQuiteMakeTheCut()
    {
        // It is the furthest chart in reach, so it scores under any floor worth having and
        // the near-miss machinery would swallow the joke. It is not competing.
        var anchor = Guid.NewGuid();
        var good = Edge(0.9, Badge("bracket", 0.5));
        var worst = Edge(0.02, Badge("bracket", 0.01));
        SetupEdges(anchor, new[] { good, worst }, levels: null);
        _mediator.Setup(m => m.Send(It.IsAny<GetOppositeChartQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(worst);
        var cut = OpenPanel(RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor)));

        cut.Find(".chart-shelf-fopposite input").Change(true);
        cut.Find(".chart-shelf-fgo").Click();

        // One card, and it is the terrible one — not the two the graph would have shown, and
        // not tucked under the near-miss disclosure where a 0.02 would normally land.
        Assert.Contains("the least like it", cut.Markup);
        Assert.Single(cut.FindAll(".chart-shelf .chart-card-title h3"));
        Assert.Empty(cut.FindAll(".chart-shelf-nearmiss"));
    }

    [Fact]
    public void TheOppositeNeedsNoRangesAndSilencesTheOnesItCannotHonour()
    {
        // It is picked from everything in reach, so a range cannot apply to it. The toggles
        // go quiet rather than sitting there implying they do.
        var anchor = Guid.NewGuid();
        SetupEdges(anchor, Edge(0.9, Badge("bracket", 0.5)));
        var cut = OpenPanel(RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor)));

        cut.Find(".chart-shelf-fopposite input").Change(true);

        Assert.All(cut.FindAll(".chart-shelf-ftoggles input"), i => Assert.True(i.HasAttribute("disabled")));
        // No range is on, but Apply is still live — the opposite selects nothing itself.
        Assert.False(cut.Find(".chart-shelf-fgo").HasAttribute("disabled"));
    }

    [Fact]
    public void ClearingTheOppositeGoesBackToThePrecalculatedGraph()
    {
        var anchor = Guid.NewGuid();
        var good = Edge(0.9, Badge("bracket", 0.5));
        var worst = Edge(0.02, Badge("bracket", 0.01));
        SetupEdges(anchor, new[] { good, worst }, levels: null);
        _mediator.Setup(m => m.Send(It.IsAny<GetOppositeChartQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(worst);
        var cut = OpenPanel(RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor)));
        cut.Find(".chart-shelf-fopposite input").Change(true);
        cut.Find(".chart-shelf-fgo").Click();
        Assert.Contains("the least like it", cut.Markup);

        cut.Find(".chart-shelf-fclear").Click();

        Assert.DoesNotContain("the least like it", cut.Markup);
        Assert.Contains("Brackets50%", Chips(cut));
    }

    [Fact]
    public void AFilterThatFindsNothingStillSaysWhatItLookedThrough()
    {
        // A narrow filter is not a broken feature, and the difference is whether the shelf
        // can say what it searched.
        var anchor = Guid.NewGuid();
        SetupEdges(anchor, Edge(0.9, Badge("bracket", 0.5)));
        _mediator.Setup(m => m.Send(It.IsAny<GetFilteredSimilarChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FilteredSimilarChartsRecord(Array.Empty<ChartSimilarityRecord>(), ChartsCompared: 128));
        var cut = OpenPanel(RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor)));
        SwitchOn(cut, Folder);

        cut.Find(".chart-shelf-fgo").Click();

        Assert.Contains("Compared 128 charts", cut.Markup);
        Assert.Contains("none cleared the bar", cut.Markup);
    }

    [Fact]
    public void AnEmptyGraphExplainsItselfInsteadOfRenderingNothing()
    {
        var anchor = Guid.NewGuid();
        SetupEdges(anchor);

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.Contains("Not enough data yet to name similar charts", cut.Markup);
    }

    private static string[] CardTitles(IRenderedFragment cut)
    {
        return cut.FindAll(".chart-shelf .chart-card-title h3").Select(e => e.TextContent.Trim()).ToArray();
    }

    private void SetupBlend(params (Guid ChartId, TierListCategory Category)[] entries)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetBlendedTierListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierListResult(
                entries.Select(e => new SongTierListEntry("Pass", e.ChartId, e.Category, 0)).ToArray(),
                IsProvisionalFallback: false));
    }

    [Fact]
    public void PersonalizingNeedsASignInButTheLensesNeverDo()
    {
        var anchor = Guid.NewGuid();
        _currentUser.SetupGet(u => u.IsLoggedIn).Returns(false);
        SetupEdges(anchor, Edge(0.9, Badge("bracket", 0.5)));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        // Pass and Score difficulty are community facts — always available. Only bending
        // them toward the reader needs an account.
        Assert.All(cut.FindAll(".chart-shelf-sort button"), b => Assert.False(b.HasAttribute("disabled")));
        Assert.True(cut.Find(".chart-shelf-personal input").HasAttribute("disabled"));
    }

    [Fact]
    public void SigningInUnlocksPersonalization()
    {
        var anchor = Guid.NewGuid();
        _currentUser.SetupGet(u => u.IsLoggedIn).Returns(true);
        SetupEdges(anchor, Edge(0.9, Badge("bracket", 0.5)));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.False(cut.Find(".chart-shelf-personal input").HasAttribute("disabled"));
    }

    [Fact]
    public void NoSortingShowsTheMatchOrderItself()
    {
        // Every difficulty sort reorders away from the true match order — this is the only
        // option that shows it, so it must not be quietly re-ranked.
        var anchor = Guid.NewGuid();
        var best = Edge(0.90, Badge("bracket", 0.5));
        var worst = Edge(0.60, Badge("bracket", 0.5));
        SetupEdges(anchor, worst, best);

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));
        cut.FindAll(".chart-shelf-sort button")[0].Click();

        // The graph already ordered them; the shelf hands them over untouched.
        Assert.Equal(new[] { $"Song {worst.ChartId:N}", $"Song {best.ChartId:N}" }, CardTitles(cut));
    }

    [Fact]
    public void ScoreDifficultyOrdersByScoringLevelEasiestFirst()
    {
        var anchor = Guid.NewGuid();
        var harder = Edge(0.90, Badge("bracket", 0.5));
        var easier = Edge(0.60, Badge("bracket", 0.5));
        SetupEdges(anchor, harder, easier);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>
            {
                [anchor] = 21.0, [easier.ChartId] = 20.4, [harder.ChartId] = 22.6
            });

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));
        cut.FindAll(".chart-shelf-sort button")[2].Click();

        // The weaker match leads because it is the easier chart — the ordering is difficulty,
        // not match strength.
        Assert.Equal(new[] { $"Song {easier.ChartId:N}", $"Song {harder.ChartId:N}" }, CardTitles(cut));
    }

    [Fact]
    public void PassDifficultyProjectsTheFolderRelativeTierOntoTheLevelScale()
    {
        // The whole cross-folder problem in one case: a D21 the list calls Underrated
        // (21.45) against a D22 it calls Overrated (21.55). The tier list ranks within a
        // folder, so its own Order cannot say which of these is harder — the projection can,
        // and it puts the D21 first by a tenth. Sorting on the raw category would have said
        // "Overrated beats Underrated" and inverted them.
        var anchor = Guid.NewGuid();
        var hardD21 = Edge(0.90, Badge("bracket", 0.5));
        var easyD22 = Edge(0.60, Badge("bracket", 0.5));
        SetupEdges(anchor, new[] { hardD21, easyD22 }, new Dictionary<Guid, int>
        {
            [hardD21.ChartId] = 21, [easyD22.ChartId] = 22
        });
        SetupBlend((hardD21.ChartId, TierListCategory.Underrated), (easyD22.ChartId, TierListCategory.Overrated));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.Equal(new[] { $"Song {hardD21.ChartId:N}", $"Song {easyD22.ChartId:N}" }, CardTitles(cut));
    }

    [Fact]
    public void AChartTheTierListCannotPlaceSortsLastRatherThanReadingAsEasy()
    {
        var anchor = Guid.NewGuid();
        var placed = Edge(0.60, Badge("bracket", 0.5));
        var unplaced = Edge(0.90, Badge("bracket", 0.5));
        SetupEdges(anchor, unplaced, placed);
        SetupBlend((placed.ChartId, TierListCategory.VeryHard));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.Equal(new[] { $"Song {placed.ChartId:N}", $"Song {unplaced.ChartId:N}" }, CardTitles(cut));
    }

    private IRenderedComponent<SimilarChartCard> RenderCard(string? videoUrl = null,
        ChartIntensityFacts? anchorIntensity = null, ChartIntensityFacts? intensity = null,
        params ChartSharedBadgeRecord[] badges)
    {
        var record = Edge(0.8, badges);
        return RenderComponent<SimilarChartCard>(p => p
            .Add(c => c.Chart, ChartSlugsTests.BuildChart(record.ChartId, song: "Neighbour"))
            .Add(c => c.Record, record)
            .Add(c => c.VideoUrl, videoUrl)
            .Add(c => c.AnchorIntensity, anchorIntensity)
            .Add(c => c.Intensity, intensity));
    }

    [Fact]
    public void TheNameCarriesARealLinkSoCrawlersFollowIt()
    {
        // The shelf IS the internal-link mesh. The link is small now — an icon beside the
        // name rather than the whole card — but it stays a real <a href>, or the SEO reason
        // for the entire feature evaporates.
        var cut = RenderCard(badges: Badge("bracket", 0.5));

        var link = cut.Find(".chart-card-title a.chart-card-golink");
        Assert.StartsWith("/Chart/", link.GetAttribute("href"));
    }

    [Fact]
    public void TheJacketPlaysAndTheLinkIsNowhereNearIt()
    {
        // Two disjoint targets, which is what dissolved the old problem: the play control
        // used to be a sibling overlaid on an <a> that owned every pixel it wanted.
        var cut = RenderCard(videoUrl: "https://example.invalid/v", badges: Badge("bracket", 0.5));

        Assert.Single(cut.FindAll("button.chart-card-art-playable"));
        Assert.Empty(cut.FindAll("button.chart-card-art a"));
        Assert.Empty(cut.FindAll("a .chart-card-art"));
    }

    [Fact]
    public void NoVideoMeansAnInertJacketRatherThanADeadButton()
    {
        var cut = RenderCard(badges: Badge("bracket", 0.5));

        Assert.Empty(cut.FindAll("button.chart-card-art"));
        Assert.Single(cut.FindAll("div.chart-card-art"));
    }

    [Fact]
    public void NothingLoadsUntilAsked()
    {
        // Six embeds on page load would outweigh the rest of the page.
        var cut = RenderCard(videoUrl: "https://example.invalid/v", badges: Badge("bracket", 0.5));

        Assert.Empty(cut.FindAll("iframe"));

        cut.Find("button.chart-card-art-playable").Click();

        Assert.Equal("https://example.invalid/v?autoplay=1",
            cut.Find("iframe.chart-card-video").GetAttribute("src"));
    }

    [Fact]
    public void TheShelfHandsTheCardARealVideoUrl()
    {
        // Regression: the shelf passed VideoUrl unprefixed. It is a STRING parameter, so
        // Blazor read the value as literal text and every card asked the origin for
        // "/entry.VideoUrl?autoplay=1". Rendering the card directly cannot catch this — the
        // typed parameter API bypasses the markup that was wrong.
        var anchor = Guid.NewGuid();
        var edge = Edge(0.9, Badge("bracket", 0.5));
        SetupEdges(anchor, edge);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartVideosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ChartVideoInformation>)new[]
            {
                new ChartVideoInformation(edge.ChartId, new Uri("https://example.invalid/watch"),
                    Name.From("Channel"))
            });

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));
        cut.Find("button.chart-card-art-playable").Click();

        Assert.Equal("https://example.invalid/watch?autoplay=1",
            cut.Find("iframe.chart-card-video").GetAttribute("src"));
    }

    [Fact]
    public void TheJacketAndTheBubbleAreSeparatelyAddressable()
    {
        // The bubble is an <img> inside the art box, so a rule aimed at the art box hits it
        // too and blows it up to the width of the card. The jacket wrapper is what keeps
        // "fill the 16:9 box" addressable to the jacket alone — if it goes, so does the
        // only thing stopping that.
        var cut = RenderCard(badges: Badge("bracket", 0.5));

        Assert.Single(cut.FindAll(".chart-card-art > .chart-card-jacket"));
        Assert.Empty(cut.FindAll(".chart-card-jacket .chart-card-bubble"));
    }

    [Fact]
    public void TheBubbleLeavesWithTheJacketWhenTheVideoStarts()
    {
        // It labels the jacket. Over a video it is just something sitting on the picture.
        var cut = RenderCard(videoUrl: "https://example.invalid/v", badges: Badge("bracket", 0.5));
        Assert.Single(cut.FindAll(".chart-card-bubble"));

        cut.Find("button.chart-card-art-playable").Click();

        Assert.Empty(cut.FindAll(".chart-card-bubble"));
    }

    [Fact]
    public void WatchingReplacesTheArtRatherThanGrowingTheCard()
    {
        // The video takes the art's exact 16:9 box, so the grid never jumps.
        var cut = RenderCard(videoUrl: "https://example.invalid/v", badges: Badge("bracket", 0.5));
        cut.Find("button.chart-card-art-playable").Click();

        Assert.Single(cut.FindAll(".chart-card-art-playing"));
        // The body is unchanged underneath: only the art's contents swapped, and the way
        // out is still there.
        Assert.Contains("Brackets50%", Chips(cut));
        Assert.Single(cut.FindAll("a.chart-card-golink"));
    }

    [Fact]
    public void IntensityChipsNameAllThreeDimensionsWithBothSides()
    {
        // "NPS" alone is a label; "10.7 → 11.9" is a fact the reader can judge — and the
        // anchor is a chart they know, so two numbers beat a verdict. Sustain and burst
        // get the same treatment: they are what the formula actually scores.
        var cut = RenderCard(
            anchorIntensity: new ChartIntensityFacts(10.7m, 0.217, 0.250),
            intensity: new ChartIntensityFacts(11.9m, 0.242, 0.148),
            badges: Badge("bracket", 0.5));

        var chips = cut.FindAll(".chart-card-why-intensity").Select(e => e.TextContent.Trim()).ToArray();
        Assert.Equal(new[] { "NPS10.7 → 11.9", "Sustain22% → 24%", "Bursts25% → 15%" }, chips);
    }

    [Fact]
    public void AnIntensityDimensionWithOnlyOneSideSaysNothing()
    {
        // A dimension needs both ends to be a comparison at all.
        var cut = RenderCard(
            anchorIntensity: new ChartIntensityFacts(10.7m, 0.217, null),
            intensity: new ChartIntensityFacts(11.9m, null, 0.148),
            badges: Badge("bracket", 0.5));

        var chips = cut.FindAll(".chart-card-why-intensity").Select(e => e.TextContent.Trim()).ToArray();
        Assert.Equal(new[] { "NPS10.7 → 11.9" }, chips);
    }

    [Fact]
    public void NoStepAnalysisMeansNoIntensityChips()
    {
        var cut = RenderCard(badges: Badge("bracket", 0.5));

        Assert.Empty(cut.FindAll(".chart-card-why-intensity"));
    }
}
