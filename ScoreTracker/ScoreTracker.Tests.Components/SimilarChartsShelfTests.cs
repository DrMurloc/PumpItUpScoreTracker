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
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityTierListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<SongTierListEntry>)Array.Empty<SongTierListEntry>());
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
        _mediator.Setup(m => m.Send(It.Is<GetSimilarChartsQuery>(q => q.ChartId == chartId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(edges);
        var charts = new List<Chart> { ChartSlugsTests.BuildChart(chartId, song: "Anchor") };
        foreach (var edge in edges) charts.Add(ChartSlugsTests.BuildChart(edge.ChartId, song: $"Song {edge.ChartId:N}"));
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

    [Fact]
    public void PersonalSortsNeedASignInAndSaySo()
    {
        var anchor = Guid.NewGuid();
        _currentUser.SetupGet(u => u.IsLoggedIn).Returns(false);
        SetupEdges(anchor, Edge(0.9, Badge("bracket", 0.5)));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        var buttons = cut.FindAll(".chart-shelf-sort button");
        // No sorting and Community always work; the two personal orders need a tier list.
        Assert.False(buttons[0].HasAttribute("disabled"));
        Assert.False(buttons[1].HasAttribute("disabled"));
        Assert.True(buttons[2].HasAttribute("disabled"));
        Assert.True(buttons[3].HasAttribute("disabled"));
    }

    [Fact]
    public void SigningInUnlocksTheTierListSorts()
    {
        var anchor = Guid.NewGuid();
        _currentUser.SetupGet(u => u.IsLoggedIn).Returns(true);
        SetupEdges(anchor, Edge(0.9, Badge("bracket", 0.5)));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.All(cut.FindAll(".chart-shelf-sort button"), b => Assert.False(b.HasAttribute("disabled")));
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
    public void CommunitySortOrdersByScoringLevelClosenessToTheAnchor()
    {
        var anchor = Guid.NewGuid();
        var far = Edge(0.90, Badge("bracket", 0.5));
        var near = Edge(0.60, Badge("bracket", 0.5));
        SetupEdges(anchor, far, near);
        // The anchor sits at 21.0; `near` is a tenth away and `far` is a full level away,
        // so the weaker match leads despite scoring lower.
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>
            {
                [anchor] = 21.0, [near.ChartId] = 21.1, [far.ChartId] = 22.0
            });

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.Equal(new[] { $"Song {near.ChartId:N}", $"Song {far.ChartId:N}" }, CardTitles(cut));
    }

    [Fact]
    public void CommunitySortFallsBackToTheTierListWhereNoScoringLevelExists()
    {
        // ~46% of the catalog has no scoring level. A chart that has one is ordered by it
        // and ranks ahead of one that can only be placed by its coarse tier bucket.
        var anchor = Guid.NewGuid();
        var scored = Edge(0.60, Badge("bracket", 0.5));
        var tieredOnly = Edge(0.90, Badge("bracket", 0.5));
        SetupEdges(anchor, tieredOnly, scored);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double> { [anchor] = 21.0, [scored.ChartId] = 23.0 });
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityTierListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<SongTierListEntry>)new[]
            {
                new SongTierListEntry("Pass Count", anchor, TierListCategory.Medium, 1),
                new SongTierListEntry("Pass Count", tieredOnly.ChartId, TierListCategory.Medium, 2)
            });

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        // `scored` leads on a two-level gap; `tieredOnly` is an exact tier match but the
        // coarser instrument, so it cannot jump the one we can actually measure.
        Assert.Equal(new[] { $"Song {scored.ChartId:N}", $"Song {tieredOnly.ChartId:N}" }, CardTitles(cut));
    }

    private IRenderedComponent<SimilarChartCard> RenderCard(string? videoUrl = null,
        decimal? anchorNps = null, decimal? nps = null, params ChartSharedBadgeRecord[] badges)
    {
        var record = Edge(0.8, badges);
        return RenderComponent<SimilarChartCard>(p => p
            .Add(c => c.Chart, ChartSlugsTests.BuildChart(record.ChartId, song: "Neighbour"))
            .Add(c => c.Record, record)
            .Add(c => c.VideoUrl, videoUrl)
            .Add(c => c.AnchorNps, anchorNps)
            .Add(c => c.Nps, nps));
    }

    [Fact]
    public void TheCardIsARealLinkSoCrawlersFollowIt()
    {
        // The shelf IS the internal-link mesh. If this ever becomes a click handler, the
        // SEO reason for the whole feature evaporates.
        var cut = RenderCard(badges: Badge("bracket", 0.5));

        var link = cut.Find("a.chart-card-link");
        Assert.StartsWith("/Chart/", link.GetAttribute("href"));
    }

    [Fact]
    public void ThePlayButtonIsASiblingOfTheLinkRatherThanInsideIt()
    {
        // A <button> inside an <a> is invalid HTML and the click targets collide.
        var cut = RenderCard(videoUrl: "https://example.invalid/v", badges: Badge("bracket", 0.5));

        Assert.Empty(cut.FindAll("a.chart-card-link button"));
        Assert.Single(cut.FindAll("div.chart-card > .chart-card-playzone > button.chart-card-play"));
    }

    [Fact]
    public void NoVideoMeansNoPlayAffordanceRatherThanADeadOne()
    {
        var cut = RenderCard(badges: Badge("bracket", 0.5));

        Assert.Empty(cut.FindAll("button.chart-card-play"));
    }

    [Fact]
    public void NothingLoadsUntilAsked()
    {
        // Six embeds on page load would outweigh the rest of the page.
        var cut = RenderCard(videoUrl: "https://example.invalid/v", badges: Badge("bracket", 0.5));

        Assert.Empty(cut.FindAll("iframe"));

        cut.Find("button.chart-card-play").Click();

        var frame = cut.Find("iframe.chart-card-video");
        Assert.Contains("https://example.invalid/v", frame.GetAttribute("src"));
    }

    [Fact]
    public void WatchingReplacesTheArtRatherThanGrowingTheCard()
    {
        // The video takes the art's exact 16:9 box, so the grid never jumps — and the
        // link is gone while playing, because the card is now a player.
        var cut = RenderCard(videoUrl: "https://example.invalid/v", badges: Badge("bracket", 0.5));
        cut.Find("button.chart-card-play").Click();

        Assert.Empty(cut.FindAll("a.chart-card-link"));
        Assert.Single(cut.FindAll(".chart-card-art-playing"));
        // The body is unchanged underneath: only the art's contents swapped.
        Assert.Contains("Brackets50%", Chips(cut));
    }

    [Fact]
    public void TheIntensityChipNamesBothSidesOrDoesNotRender()
    {
        // "NPS" alone is a label; "10.7 → 11.9" is a fact the reader can judge. Without
        // both sides there is nothing honest to say.
        var withBoth = RenderCard(anchorNps: 10.7m, nps: 11.9m, badges: Badge("bracket", 0.5));
        Assert.Contains("10.7 → 11.9", withBoth.Markup);

        var anchorOnly = RenderCard(anchorNps: 10.7m, badges: Badge("bracket", 0.5));
        Assert.DoesNotContain("10.7", anchorOnly.Markup);
    }
}
