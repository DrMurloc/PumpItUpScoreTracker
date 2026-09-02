using System;
using System.Linq;
using ScoreTracker.SharedKernel.Models;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public class BreakPositionSolverTests
{
    [Fact]
    public void PlacesAtTheJthEventWhenTheFileMatchesTheGame()
    {
        var events = new decimal[] { 1.0m, 2.0m, 3.0m, 4.0m, 5.0m };

        Assert.Equal(3.0m, BreakPositionSolver.Place(3, events, 5));
    }

    [Fact]
    public void FollowsDensityInsteadOfTheClock()
    {
        // Nine of ten judgements land in the first two seconds; the tenth waits until t=60.
        // Half the judgements is therefore EARLY, nowhere near half the clock.
        var events = new[] { 0.2m, 0.4m, 0.6m, 0.8m, 1.0m, 1.2m, 1.4m, 1.6m, 1.8m, 60m };

        Assert.Equal(1.0m, BreakPositionSolver.Place(5, events, 10));
    }

    [Fact]
    public void RescalesThroughTheImpliedTotalWhenTheFileOverCounts()
    {
        // File implies 12 events against a judged total of 10 (within the gate): a full run
        // must land on the final event, not fall two short.
        var events = Enumerable.Range(1, 12).Select(i => (decimal)i).ToArray();

        Assert.Equal(12m, BreakPositionSolver.Place(10, events, 10));
        Assert.Equal(6m, BreakPositionSolver.Place(5, events, 10));
    }

    [Fact]
    public void ClampsAJudgedCountPastTheTotalToTheFinalEvent()
    {
        var events = new decimal[] { 1m, 2m, 3m };

        Assert.Equal(3m, BreakPositionSolver.Place(9, events, 3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void RefusesAJudgedCountThatSaysNothing(int judged)
    {
        Assert.Null(BreakPositionSolver.Place(judged, new decimal[] { 1m }, 10));
    }

    [Fact]
    public void RefusesAnEmptyTimelineAndAMissingTotal()
    {
        Assert.Null(BreakPositionSolver.Place(3, Array.Empty<decimal>(), 10));
        Assert.Null(BreakPositionSolver.Place(3, new decimal[] { 1m }, 0));
    }

    [Fact]
    public void StacksRunsThatDiedOnTheSameNoteIntoOnePin()
    {
        // Three runs placed on the same judgement event share the pin; the loner keeps its own.
        var pins = BreakPositionSolver.Cluster(new[] { 71.2m, 71.2m, 71.2m, 16.3m });

        Assert.Equal(2, pins.Count);
        Assert.Equal(1, pins[0].Count);
        Assert.Equal(16.3m, pins[0].Time);
        Assert.Equal(3, pins[1].Count);
        Assert.Equal(71.2m, pins[1].From);
        Assert.Equal(71.2m, pins[1].To);
    }

    [Fact]
    public void NeighbouringNotesNeverShareAPin()
    {
        // A second is a long time in a rhythm game (owner, 2026-08-31): deaths on adjacent
        // notes are distinct spikes, never one smear — the note grid is the grouping.
        var pins = BreakPositionSolver.Cluster(new[] { 10m, 11m, 12m, 13m });

        Assert.Equal(4, pins.Count);
        Assert.All(pins, pin => Assert.Equal(1, pin.Count));
    }

    [Fact]
    public void OrdersPinsByTime()
    {
        var pins = BreakPositionSolver.Cluster(new[] { 50m, 20m, 20m, 90m });

        Assert.Equal(3, pins.Count);
        Assert.Equal(20m, pins[0].Time);
        Assert.Equal(2, pins[0].Count);
        Assert.Equal(50m, pins[1].Time);
        Assert.Equal(90m, pins[2].Time);
    }

    [Fact]
    public void AnEmptySetOfDeathsIsAnEmptyRail()
    {
        Assert.Empty(BreakPositionSolver.Cluster(Array.Empty<decimal>()));
    }
}
