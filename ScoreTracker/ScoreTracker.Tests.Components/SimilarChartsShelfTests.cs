using System;
using System.Collections.Generic;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using MudBlazor.Services;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
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

    public SimilarChartsShelfTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(o => o.PopoverOptions.CheckForPopoverProvider = false);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        Services.AddSingleton(_mediator.Object);
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
        var charts = new List<Chart>();
        foreach (var edge in edges) charts.Add(ChartSlugsTests.BuildChart(edge.ChartId, song: $"Song {edge.ChartId:N}"));
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
    }

    [Fact]
    public void ChipsNameTheBadgesThePairSharesAtTheirSharedCoverage()
    {
        var anchor = Guid.NewGuid();
        // "Brackets 50%" means BOTH charts are at least half brackets — the shared
        // coverage, not either chart's own. Named badges, never "skills match".
        SetupEdges(anchor, Edge(0.9, Badge("bracket", 0.5), Badge("anchor_run", 0.25)));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.Contains("Brackets 50%", cut.Markup);
        Assert.Contains("Anchor runs 25%", cut.Markup);
        Assert.DoesNotContain("same skill profile", cut.Markup);
    }

    [Fact]
    public void ATraceOfABadgeIsNotAReason()
    {
        var anchor = Guid.NewGuid();
        // Both charts brush past a drill. That is not something they have in common.
        SetupEdges(anchor, Edge(0.9, Badge("bracket", 0.5), Badge("drill", 0.04)));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.Contains("Brackets 50%", cut.Markup);
        Assert.DoesNotContain("Drills", cut.Markup);
    }

    [Fact]
    public void AtMostThreeChipsRender()
    {
        var anchor = Guid.NewGuid();
        SetupEdges(anchor, Edge(0.9, Badge("bracket", 0.9), Badge("anchor_run", 0.8),
            Badge("drill", 0.7), Badge("jack", 0.6)));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.Contains("Brackets 90%", cut.Markup);
        Assert.Contains("Anchor runs 80%", cut.Markup);
        Assert.Contains("Drills 70%", cut.Markup);
        Assert.DoesNotContain("Jacks", cut.Markup);
    }

    [Fact]
    public void AnUnmappedBadgeFallsBackToItsRawNameRatherThanVanishing()
    {
        var anchor = Guid.NewGuid();
        SetupEdges(anchor, Edge(0.9, Badge("some_new_piucenter_badge", 0.5)));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.Contains("some_new_piucenter_badge 50%", cut.Markup);
    }

    [Fact]
    public void EdgesUnderTheRenderFloorAreNotMatches()
    {
        var anchor = Guid.NewGuid();
        // The graph stores its whole tail floor-free; deciding what counts as a match is
        // the shelf's job, so a tail row must not render as one.
        SetupEdges(anchor, Edge(0.30, Badge("bracket", 0.5)));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.Contains("Not enough data yet to name similar charts", cut.Markup);
        Assert.DoesNotContain("Brackets", cut.Markup);
    }

    [Fact]
    public void AnEmptyGraphExplainsItselfInsteadOfRenderingNothing()
    {
        var anchor = Guid.NewGuid();
        SetupEdges(anchor);

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.Contains("Not enough data yet to name similar charts", cut.Markup);
    }
}
