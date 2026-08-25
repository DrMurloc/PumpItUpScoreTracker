using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The folder context every chip is read against (docs/design/chart-identity.md §5).
/// </summary>
public sealed class FolderBaselineBuilderTests
{
    private static ChartBadgeProfile Chart(params (string Badge, decimal Coverage)[] coverage)
    {
        return new ChartBadgeProfile(Guid.NewGuid(),
            coverage.ToDictionary(c => c.Badge, c => c.Coverage, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), null, Array.Empty<string>());
    }

    private static IReadOnlyList<ChartFolderBaseline> Build(params ChartBadgeProfile[] charts)
    {
        return FolderBaselineBuilder.Build(MixEnum.Phoenix, ChartType.Double, 24, charts);
    }

    [Fact]
    public void ABadgeMostOfTheFolderLacksCountsAsUniqueAndOneEverybodyHasDoesNot()
    {
        // Ten charts: one carries splits, nine carry mid-6.
        var charts = Enumerable.Range(0, 9).Select(_ => Chart(("mid6_doubles", 0.5m)))
            .Append(Chart(("split", 0.5m)))
            .ToArray();

        var baselines = Build(charts).ToDictionary(b => b.Badge);

        Assert.True(baselines["split"].IsUniqueInFolder);
        Assert.False(baselines["mid6_doubles"].IsUniqueInFolder);
        Assert.Equal(10, baselines["split"].AnalyzedCharts);
        Assert.Equal(1, baselines["split"].QualifiedCount);
    }

    /// <summary>
    ///     The floor is what makes a rare badge's zero-valued cutoff safe. Nine charts have no
    ///     brackets at all, so the folder's 75th-percentile bracket coverage is zero — without
    ///     the floor every chart in the folder would read as core for brackets.
    /// </summary>
    [Fact]
    public void ChartsWithoutTheBadgeAreNotCoreEvenWhenTheFolderCutoffIsZero()
    {
        var carrier = Chart(("bracket", 0.5m));
        var charts = Enumerable.Range(0, 9).Select(_ => Chart(("run", 0.5m))).Append(carrier).ToArray();

        var bracket = Build(charts).Single(b => b.Badge == "bracket");

        Assert.Equal(0m, bracket.CoreCutoff);
        Assert.True(bracket.IsCore(0.5m));
        Assert.False(bracket.IsCore(0m));
        Assert.False(bracket.IsCore(0.10m));
    }

    /// <summary>
    ///     Core is "far more of this than the folder carries", so a chart sitting at the
    ///     folder's own norm does not qualify however much of the badge it has.
    /// </summary>
    [Fact]
    public void ACoverageMatchingTheFolderNormIsNotCore()
    {
        var charts = Enumerable.Range(0, 10).Select(_ => Chart(("run", 0.5m))).ToArray();

        var run = Build(charts).Single(b => b.Badge == "run");

        Assert.False(run.IsCore(0.5m));
        Assert.True(run.IsCore(0.6m));
    }

    [Fact]
    public void AnEmptyFolderProducesNoBaselines()
    {
        Assert.Empty(Build());
    }

    /// <summary>
    ///     Whole-chart badges never carry coverage, so their presence is the dominance pick —
    ///     they still get a prevalence, which is what the ✦ rule reads.
    /// </summary>
    [Fact]
    public void WholeChartBadgesCountTowardPrevalenceThroughTheirPick()
    {
        var sustained = new ChartBadgeProfile(Guid.NewGuid(),
            new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["sustained"] = 1 }, null,
            Array.Empty<string>());
        var charts = Enumerable.Range(0, 9).Select(_ => Chart(("run", 0.5m))).Append(sustained).ToArray();

        var baseline = Build(charts).Single(b => b.Badge == "sustained");

        Assert.Equal(1, baseline.QualifiedCount);
        Assert.True(baseline.IsUniqueInFolder);
    }
}
