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
    public void WhyChipsFollowTheDesignDocThresholds()
    {
        var anchor = Guid.NewGuid();
        // Skill clears its bar, intensity misses: the pair asks for the same things but
        // asks rather differently hard, and only the true claim gets a chip.
        SetupEdges(anchor, new ChartSimilarityRecord(Guid.NewGuid(), 0.9,
            SkillScore: 0.8, IntensityScore: 0.5));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.Contains("same skill profile", cut.Markup);
        Assert.DoesNotContain("same intensity", cut.Markup);
    }

    [Fact]
    public void NoQualifyingSignalFallsBackToTheLevelNeighborChip()
    {
        var anchor = Guid.NewGuid();
        SetupEdges(anchor, new ChartSimilarityRecord(Guid.NewGuid(), 0.6,
            SkillScore: 0.6, IntensityScore: 0.5));

        var cut = RenderComponent<SimilarChartsShelf>(p => p.Add(s => s.ChartId, anchor));

        Assert.Contains("level neighbor", cut.Markup);
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
