using System;
using System.Linq;
using ScoreTracker.Catalog.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     D29's five tests (docs/design/march-of-murlocs.md). Every case below is a production
///     measurement from 김재현's real Doubles record book — the same pull the rule was derived
///     against — replayed through the rule, so the four he accepted have to pass and the two he
///     rejected have to fail, each for the reason he gave.
/// </summary>
public sealed class RestChartRuleTests
{
    private static RestChartMeasures Measured(double tps, int stepsPct, double hold, int holdPct,
        double hardTwist, double crux, int cruxPct, bool drillOrAnchor = false) =>
        new(tps, stepsPct, hold, holdPct, hardTwist, crux, cruxPct, drillOrAnchor);

    [Fact]
    public void EveryChartTheOwnerAcceptedPassesAllFiveTests()
    {
        // Slam D24, Queencard D22, 8 6 FULL SONG D23, Pop Sequence D23 -- the four of his list
        // that sit in the record book this was measured against.
        var accepted = new (string Name, RestChartMeasures Measures)[]
        {
            ("Slam D24", Measured(4.727, 6, 0.5339, 79, 0.0000, 2.9, 7)),
            ("Queencard D22", Measured(3.755, 6, 0.6652, 95, 0.5000, 3.1, 39)),
            ("8 6 - FULL SONG - D23", Measured(2.737, 1, 0.8180, 100, 0.1667, 2.4, 3)),
            ("Pop Sequence D23", Measured(3.992, 6, 0.6346, 90, 0.0000, 3.4, 41))
        };

        var rejected = accepted.Where(c => !RestChartRule.IsRest(c.Measures)).Select(c => c.Name).ToArray();

        Assert.Empty(rejected);
    }

    [Fact]
    public void V3IsRejectedForItsTwistsAndNothingElse()
    {
        // Step-light, hold-heavy, no drills, crux mid-folder -- it passes four of five. Its hard
        // twists cover 1.43 of the chart against at most 0.50 for everything accepted.
        var v3 = Measured(4.154, 3, 0.6396, 97, 1.4286, 4.4, 52);

        Assert.False(RestChartRule.IsRest(v3));
        Assert.True(v3.StepsPercentile <= RestChartRule.MaxStepsPercentile);
        Assert.True(v3.HoldPercentile >= RestChartRule.MinHoldPercentile);
        Assert.True(v3.CruxPercentile <= RestChartRule.MaxCruxPercentile);
        Assert.True(v3.HardTwistShare > RestChartRule.MaxHardTwistShare);
    }

    [Fact]
    public void FourNTIsRejectedForItsCruxAndNothingElse()
    {
        // Its crux sits at the 73rd percentile of D24 where every accepted chart is at or below
        // the 59th; on every other axis it looks like a rest chart.
        var fourNt = Measured(4.971, 11, 0.5650, 87, 0.2858, 6.1, 73);

        Assert.False(RestChartRule.IsRest(fourNt));
        Assert.True(fourNt.StepsPercentile <= RestChartRule.MaxStepsPercentile);
        Assert.True(fourNt.HoldPercentile >= RestChartRule.MinHoldPercentile);
        Assert.True(fourNt.HardTwistShare <= RestChartRule.MaxHardTwistShare);
        Assert.True(fourNt.CruxPercentile > RestChartRule.MaxCruxPercentile);
    }

    [Fact]
    public void HalfTheChartInHardTwistsIsStillRestAndAnythingMoreIsNot()
    {
        // Queencard sits exactly on the line, which is why the test is "at most".
        var onTheLine = Measured(3.755, 6, 0.6652, 95, 0.50, 3.1, 39);

        Assert.True(RestChartRule.IsRest(onTheLine));
        Assert.False(RestChartRule.IsRest(onTheLine with { HardTwistShare = 0.51 }));
    }

    [Fact]
    public void ADrillOrAnAnchorRunDisqualifiesOnItsOwn()
    {
        var restful = Measured(2.737, 1, 0.8180, 100, 0.0, 2.4, 3);

        Assert.True(RestChartRule.IsRest(restful));
        Assert.False(RestChartRule.IsRest(restful with { HasDrillOrAnchorRun = true }));
    }

    [Fact]
    public void TheStepAndHoldGatesAreTheQuartersTheyClaimToBe()
    {
        var restful = Measured(2.737, 1, 0.8180, 100, 0.0, 2.4, 3);

        Assert.True(RestChartRule.IsRest(restful with { StepsPercentile = 25 }));
        Assert.False(RestChartRule.IsRest(restful with { StepsPercentile = 26 }));
        Assert.True(RestChartRule.IsRest(restful with { HoldPercentile = 75 }));
        Assert.False(RestChartRule.IsRest(restful with { HoldPercentile = 74 }));
        Assert.True(RestChartRule.IsRest(restful with { CruxPercentile = 60 }));
        Assert.False(RestChartRule.IsRest(restful with { CruxPercentile = 61 }));
    }

    [Fact]
    public void APercentileIsTheShareOfTheFolderBelowIt()
    {
        var folder = new[] { 1.0, 2.0, 3.0, 4.0 };

        Assert.Equal(0, RestChartRule.Percentile(folder, 1.0));
        Assert.Equal(25, RestChartRule.Percentile(folder, 2.0));
        Assert.Equal(75, RestChartRule.Percentile(folder, 4.0));
        // A folder of one has no distribution, so nothing in it can be top-quarter anything.
        Assert.Equal(0, RestChartRule.Percentile(new[] { 5.0 }, 5.0));
        Assert.Equal(0, RestChartRule.Percentile(Array.Empty<double>(), 5.0));
    }
}
