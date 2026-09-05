using System;
using ScoreTracker.Domain.Models;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class PeerStandingTests
{
    private static PeerStandingSource[] NoSources => Array.Empty<PeerStandingSource>();

    [Fact]
    public void APlaceIsReadInsideAPopulationThatIncludesYou()
    {
        // 93 peers passed it, 5 of them scored higher: you are #6 of 94.
        var standing = new PeerStanding(120, 93, 5, 0, 4, NoSources, null);

        Assert.Equal(6, standing.Place);
        Assert.Equal(94, standing.Cohort);
        Assert.Equal(27, standing.NotPassed);
    }

    [Fact]
    public void ThePercentileIsTheTieInclusiveShareAtOrBelowYou()
    {
        var standing = new PeerStanding(100, 93, 5, 0, 0, NoSources, null);

        Assert.Equal(89 / 94.0, standing.Percentile!.Value, 10);
    }

    [Fact]
    public void FirstPlaceIsAFullPercentile()
    {
        var standing = new PeerStanding(50, 40, 0, 0, 0, NoSources, null);

        Assert.True(standing.IsFirst);
        Assert.Equal(1.0, standing.Percentile);
    }

    [Fact]
    public void AChartNoPeerHasPassedHasNoPercentileRatherThanAFlatteringOne()
    {
        var standing = PeerStanding.NoCohort(12, broke: 3, NoSources);

        Assert.False(standing.HasCohort);
        Assert.Null(standing.Percentile);
        Assert.Equal(12, standing.NotPassed);
        Assert.Equal(3, standing.Broke);
    }

    [Fact]
    public void ASourceLineCountsOtherPeopleAndReadsItsPlaceAmongThemPlusYou()
    {
        var source = new PeerStandingSource(PeerSourceKind.Rivals, null, null, false, false,
            Members: 9, Passed: 5, Better: 1, FromOfficialBoard: 2);

        Assert.Equal(2, source.Place);
        Assert.Equal(6, source.Of);
        Assert.Equal(4, source.NotPassed);
    }
}
