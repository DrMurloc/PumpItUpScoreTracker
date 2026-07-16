using System;
using System.Linq;
using ScoreTracker.Domain.Services;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class ScoreAgePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);

    private static (Guid Key, DateTimeOffset RecordedDate) AgedScore(double daysOld)
    {
        return (Guid.NewGuid(), Now.AddDays(-daysOld));
    }

    [Fact]
    public void FreshScoresAllKeepFullWeight()
    {
        var scores = new[] { AgedScore(0), AgedScore(5), AgedScore(10) };

        var weights = ScoreAgePolicy.AgeOutlierWeights(scores, Now);

        Assert.All(scores, s => Assert.Equal(1.0, weights[s.Key]));
    }

    [Fact]
    public void UniformlyOldHistoryKeepsFullVoice()
    {
        // A returning player's coherent snapshot: every score is old, none is an
        // outlier against the rest, so nothing is diminished.
        var scores = new[] { AgedScore(400), AgedScore(400), AgedScore(400) };

        var weights = ScoreAgePolicy.AgeOutlierWeights(scores, Now);

        Assert.All(scores, s => Assert.Equal(1.0, weights[s.Key]));
    }

    [Fact]
    public void AgeOutlierIsDiminishedByHalfVoicePer180DaysPastThreshold()
    {
        // Ages 0,0,0,0,400: mean 80, population σ 160 → threshold 240. The outlier
        // sits 160 days past it → weight 0.5^(160/180).
        var fresh = new[] { AgedScore(0), AgedScore(0), AgedScore(0), AgedScore(0) };
        var outlier = AgedScore(400);

        var weights = ScoreAgePolicy.AgeOutlierWeights(fresh.Append(outlier), Now);

        Assert.All(fresh, s => Assert.Equal(1.0, weights[s.Key]));
        Assert.Equal(Math.Pow(0.5, 160.0 / 180.0), weights[outlier.Key], 9);
    }

    [Fact]
    public void ExtremeOutlierIsFlooredNotExcluded()
    {
        // Ages 0,0,0,0,3000: threshold 1800, the outlier is 1200 days past it —
        // raw diminishment would be under 1%, the floor keeps it audible.
        var fresh = new[] { AgedScore(0), AgedScore(0), AgedScore(0), AgedScore(0) };
        var outlier = AgedScore(3000);

        var weights = ScoreAgePolicy.AgeOutlierWeights(fresh.Append(outlier), Now);

        Assert.Equal(0.1, weights[outlier.Key]);
    }

    [Fact]
    public void GraceFloorProtectsYoungAccountsFromTheirOwnSpread()
    {
        // Ages 0 and 20: mean+σ is 20, but the grace floor holds the threshold at 30
        // days — a three-week-old score never reads as outdated next to yesterday's.
        var yesterday = AgedScore(0);
        var threeWeeks = AgedScore(20);

        var weights = ScoreAgePolicy.AgeOutlierWeights(new[] { yesterday, threeWeeks }, Now);

        Assert.Equal(1.0, weights[threeWeeks.Key]);
    }

    [Fact]
    public void NoScoresYieldsNoWeights()
    {
        var weights = ScoreAgePolicy.AgeOutlierWeights(
            Array.Empty<(Guid, DateTimeOffset)>(), Now);

        Assert.Empty(weights);
    }
}
