using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class LifebarSimulatorTests
{
    [Theory]
    [InlineData(15, 1675)]   // 1000 + 15*15*3
    [InlineData(20, 2200)]   // 1000 + 20*20*3
    [InlineData(29, 3523)]   // 1000 + 29*29*3
    public void MaxLifeFollowsLevelFormula(int level, int expectedMax)
    {
        var sim = new LifebarSimulator(DifficultyLevel.From(level));
        Assert.Equal(expectedMax, sim.MaxLife);
    }

    [Fact]
    public void StartAtHalfByDefault()
    {
        var sim = new LifebarSimulator(DifficultyLevel.From(20));
        Assert.Equal(500, sim.CurrentLife);
    }

    [Fact]
    public void StartAtFullWhenRequested()
    {
        var sim = new LifebarSimulator(DifficultyLevel.From(20), startAtFull: true);
        Assert.Equal(sim.MaxLife, sim.CurrentLife);
    }

    [Fact]
    public void GoodDoesNotChangeLife()
    {
        var sim = new LifebarSimulator(DifficultyLevel.From(20));
        var before = sim.CurrentLife;
        sim.ApplyJudgment(Judgment.Good);
        Assert.Equal(before, sim.CurrentLife);
    }

    [Fact]
    public void BadReducesLifeByFifty()
    {
        var sim = new LifebarSimulator(DifficultyLevel.From(20));
        sim.ApplyJudgment(Judgment.Bad);
        Assert.Equal(450, sim.CurrentLife);
    }

    [Fact]
    public void RepeatedBadsClampAtZero()
    {
        var sim = new LifebarSimulator(DifficultyLevel.From(20));
        for (var i = 0; i < 100; i++) sim.ApplyJudgment(Judgment.Bad);
        Assert.Equal(0, sim.CurrentLife);
    }

    [Fact]
    public void MissReducesLifeMoreThanBad()
    {
        var missSim = new LifebarSimulator(DifficultyLevel.From(20));
        missSim.ApplyJudgment(Judgment.Miss);

        var badSim = new LifebarSimulator(DifficultyLevel.From(20));
        badSim.ApplyJudgment(Judgment.Bad);

        Assert.True(missSim.CurrentLife < badSim.CurrentLife);
    }

    [Fact]
    public void PerfectIncreasesLife()
    {
        var sim = new LifebarSimulator(DifficultyLevel.From(20));
        var before = sim.CurrentLife;
        sim.ApplyJudgment(Judgment.Perfect);
        Assert.True(sim.CurrentLife >= before);
    }

    [Fact]
    public void RepeatedPerfectsClampAtMaxLife()
    {
        var sim = new LifebarSimulator(DifficultyLevel.From(20));
        for (var i = 0; i < 10000; i++) sim.ApplyJudgment(Judgment.Perfect);
        Assert.Equal(sim.MaxLife, sim.CurrentLife);
    }

    [Fact]
    public void MissesAfterBadsStillReduceLifeAtZero()
    {
        var sim = new LifebarSimulator(DifficultyLevel.From(20));
        for (var i = 0; i < 100; i++) sim.ApplyJudgment(Judgment.Bad);
        sim.ApplyJudgment(Judgment.Miss);
        Assert.Equal(0, sim.CurrentLife);
    }
}
