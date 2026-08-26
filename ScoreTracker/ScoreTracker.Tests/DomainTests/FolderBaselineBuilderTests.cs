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
        Assert.Equal(1, baselines["split"].PresentCount);
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
    public void WholeChartBadgesCarryNoPrevalenceBecauseTheyCarryNoCoverage()
    {
        var sustained = new ChartBadgeProfile(Guid.NewGuid(),
            new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["sustained"] = 1 }, null,
            Array.Empty<string>());
        var charts = Enumerable.Range(0, 9).Select(_ => Chart(("run", 0.5m))).Append(sustained).ToArray();

        var baseline = Build(charts).Single(b => b.Badge == "sustained");

        // No coverage anywhere means no prevalence to read, and the rare rule must not fire on
        // "only this chart was picked for it" — which is true of any pick and claims nothing.
        // Whole-chart qualities answer to their own test in the engine instead.
        Assert.Equal(0, baseline.PresentCount);
        Assert.False(baseline.IsUniqueInFolder);
    }

    /// <summary>
    ///     The presence bar is a budget, not a number (docs/design/chart-identity.md §3.1).
    ///     Owner, 2026-08-26: "A d26 with a handful of brackets but overall just being a run
    ///     shouldn't even mention the thought of brackets. A S18 with brackets probably should at
    ///     least feature them." Measured in Phoenix 2, brackets sit on 13.7% of S14 and 79.4% of
    ///     D26, so one fixed bar cannot be right for both — at S14 it sat above the entire folder
    ///     and not one chart could say it had brackets.
    /// </summary>
    [Fact]
    public void ACommonTechniqueIsHarderToClaimThanARareOne()
    {
        // Twenty charts. Brackets are everywhere at a middling share; splits are on two.
        var common = Enumerable.Range(0, 18)
            .Select(i => Chart(("bracket", 0.20m + i * 0.01m)))
            .Append(Chart(("bracket", 0.60m), ("split", 0.15m)))
            .Append(Chart(("bracket", 0.55m), ("split", 0.12m)))
            .ToArray();

        var baselines = Build(common).ToDictionary(b => b.Badge);

        // Brackets are on every chart, so only the dominant few may claim them.
        Assert.True(baselines["bracket"].PresenceCutoff > 0.30m);
        Assert.False(baselines["bracket"].IsPresent(0.28m));
        // Splits are on two of twenty, so carrying any at all is the whole story — and the bar
        // has to fall BELOW the old fixed one, or the exotic vocabulary stays invisible.
        Assert.True(baselines["split"].PresenceCutoff <= 0.12m);
        Assert.True(baselines["split"].IsPresent(0.12m));
    }

    /// <summary>
    ///     The failure this replaced: hold footslides, footswitches, hands and splits have folder
    ///     MAXIMUMS below the old fixed 0.30 bar, so no chart in any folder could ever say it had
    ///     one. Hi Bi D22 carries piucenter's #1 and #2 picks and showed neither.
    /// </summary>
    [Fact]
    public void ATechniqueWhoseFolderMaximumIsTinyIsStillClaimable()
    {
        var charts = Enumerable.Range(0, 19).Select(_ => Chart(("run", 0.5m)))
            .Append(Chart(("run", 0.5m), ("hold_footslide", 0.167m)))
            .ToArray();

        var baseline = Build(charts).Single(b => b.Badge == "hold_footslide");

        Assert.True(baseline.IsPresent(0.167m));
        Assert.True(baseline.IsUniqueInFolder);
    }
}
