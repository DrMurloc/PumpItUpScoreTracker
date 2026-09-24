using System;
using System.Linq;
using ScoreTracker.ScoreLedger.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class SittingPlannerTests
{
    private static readonly TimeSpan Gap = TimeSpan.FromMinutes(15);
    private static readonly DateTimeOffset Start = new(2026, 9, 23, 18, 0, 0, TimeSpan.Zero);

    private static readonly OpenSitting[] None = Array.Empty<OpenSitting>();

    private static DateTimeOffset At(int minutes) => Start.AddMinutes(minutes);

    [Fact]
    public void AFirstPlayStartsASitting()
    {
        var plan = SittingPlanner.Plan(None, new[] { At(0) }, Gap);

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

        var plan = SittingPlanner.Plan(new[] { open }, new[] { At(40 + minutesAfterLastPlay) }, Gap);

        Assert.Empty(plan.Opened);
        Assert.Equal(open.Id, Assert.Single(plan.SittingIds));
    }

    [Fact]
    public void APlayMoreThanTheGapAfterTheOpenSittingStartsANewOne()
    {
        var open = new OpenSitting(Guid.NewGuid(), At(0), At(40));

        var plan = SittingPlanner.Plan(new[] { open }, new[] { At(56) }, Gap);

        var opened = Assert.Single(plan.Opened);
        Assert.Equal(opened.Id, Assert.Single(plan.SittingIds));
        Assert.NotEqual(open.Id, opened.Id);
    }

    [Fact]
    public void APlayFromBeforeTheOpenSittingJoinsItOnlyWithinTheGap()
    {
        var open = new OpenSitting(Guid.NewGuid(), At(60), At(90));

        var plan = SittingPlanner.Plan(new[] { open }, new[] { At(50), At(10) }, Gap);

        Assert.Equal(open.Id, plan.SittingIds[0]);
        Assert.Equal(Assert.Single(plan.Opened).Id, plan.SittingIds[1]);
    }

    [Fact]
    public void ABacklogSplitsIntoSittingsWhereverThePlaysAreMoreThanTheGapApart()
    {
        // Two sittings a day apart, sent together and out of order.
        var plays = new[] { At(1440 + 4), At(0), At(1440), At(10), At(22) };

        var plan = SittingPlanner.Plan(None, plays, Gap);

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

        var plan = SittingPlanner.Plan(new[] { open }, new[] { At(24), At(38), At(52) }, Gap);

        Assert.Empty(plan.Opened);
        Assert.All(plan.SittingIds, id => Assert.Equal(open.Id, id));
    }

    [Fact]
    public void PlaysChainingIntoTheStartOfTheOpenSittingJoinIt()
    {
        // A tool that sends its offline plays after the live ones: the first is twenty minutes before
        // the sitting started, but the second closes the gap.
        var open = new OpenSitting(Guid.NewGuid(), At(0), At(30));

        var plan = SittingPlanner.Plan(new[] { open }, new[] { At(-20), At(-10) }, Gap);

        Assert.Empty(plan.Opened);
        Assert.All(plan.SittingIds, id => Assert.Equal(open.Id, id));
    }

    [Fact]
    public void ALivePlayFindsItsSittingBesideABacklogSentMeanwhile()
    {
        var live = new OpenSitting(Guid.NewGuid(), At(0), At(10));
        var backlog = new OpenSitting(Guid.NewGuid(), At(-1440), At(-1400));

        var plan = SittingPlanner.Plan(new[] { backlog, live }, new[] { At(20) }, Gap);

        Assert.Empty(plan.Opened);
        Assert.Equal(live.Id, Assert.Single(plan.SittingIds));
    }

    [Fact]
    public void EachOpenSittingTakesThePlaysNearestIt()
    {
        var first = new OpenSitting(Guid.NewGuid(), At(0), At(10));
        var second = new OpenSitting(Guid.NewGuid(), At(30), At(40));

        // 18 is within the gap of both, and nearer the first.
        var plan = SittingPlanner.Plan(new[] { second, first }, new[] { At(35), At(5), At(18), At(60) }, Gap);

        var opened = Assert.Single(plan.Opened);
        Assert.Equal(At(60), opened.FirstPlayedAt);
        Assert.Equal(new[] { second.Id, first.Id, first.Id, opened.Id }, plan.SittingIds.ToArray());
    }
}
