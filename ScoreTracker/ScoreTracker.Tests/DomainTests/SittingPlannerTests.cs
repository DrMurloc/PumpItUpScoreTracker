using System;
using System.Linq;
using ScoreTracker.ScoreLedger.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class SittingPlannerTests
{
    private static readonly TimeSpan Gap = TimeSpan.FromMinutes(15);
    private static readonly DateTimeOffset Start = new(2026, 9, 23, 18, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int minutes) => Start.AddMinutes(minutes);

    [Fact]
    public void AFirstPlayStartsASitting()
    {
        var plan = SittingPlanner.Plan(null, new[] { At(0) }, Gap);

        var opened = Assert.Single(plan.Opened);
        Assert.Equal(At(0), opened.FirstPlayedAt);
        Assert.Equal(opened.Id, Assert.Single(plan.SittingIds));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(15)]
    public void APlayWithinTheGapOfTheOpenSittingJoinsIt(int minutesAfterLastPlay)
    {
        var open = new OpenSitting(Guid.NewGuid(), At(0), At(40));

        var plan = SittingPlanner.Plan(open, new[] { At(40 + minutesAfterLastPlay) }, Gap);

        Assert.Empty(plan.Opened);
        Assert.Equal(open.Id, Assert.Single(plan.SittingIds));
    }

    [Fact]
    public void APlayMoreThanTheGapAfterTheOpenSittingStartsANewOne()
    {
        var open = new OpenSitting(Guid.NewGuid(), At(0), At(40));

        var plan = SittingPlanner.Plan(open, new[] { At(56) }, Gap);

        var opened = Assert.Single(plan.Opened);
        Assert.Equal(opened.Id, Assert.Single(plan.SittingIds));
        Assert.NotEqual(open.Id, opened.Id);
    }

    [Fact]
    public void APlayFromBeforeTheOpenSittingJoinsItOnlyWithinTheGap()
    {
        var open = new OpenSitting(Guid.NewGuid(), At(60), At(90));

        var plan = SittingPlanner.Plan(open, new[] { At(50), At(10) }, Gap);

        Assert.Equal(open.Id, plan.SittingIds[0]);
        Assert.Equal(Assert.Single(plan.Opened).Id, plan.SittingIds[1]);
    }

    [Fact]
    public void ABacklogSplitsIntoSittingsWhereverThePlaysAreMoreThanTheGapApart()
    {
        // Two sittings a day apart, sent together and out of order.
        var plays = new[] { At(1440 + 4), At(0), At(1440), At(10), At(22) };

        var plan = SittingPlanner.Plan(null, plays, Gap);

        Assert.Equal(2, plan.Opened.Count);
        Assert.Equal(At(0), plan.Opened[0].FirstPlayedAt);
        Assert.Equal(At(1440), plan.Opened[1].FirstPlayedAt);
        var evening = plan.Opened[0].Id;
        var nextDay = plan.Opened[1].Id;
        Assert.Equal(new[] { nextDay, evening, nextDay, evening, evening }, plan.SittingIds.ToArray());
    }

    [Fact]
    public void PlaysJoiningTheOpenSittingStretchItForTheOnesAfterThem()
    {
        var open = new OpenSitting(Guid.NewGuid(), At(0), At(10));

        var plan = SittingPlanner.Plan(open, new[] { At(24), At(38), At(52) }, Gap);

        Assert.Empty(plan.Opened);
        Assert.All(plan.SittingIds, id => Assert.Equal(open.Id, id));
    }
}
