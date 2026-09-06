using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class TournamentSessionTests
{
    [Fact]
    public void NewSessionStartsWithNoEntries()
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());

        Assert.Empty(session.Entries);
        Assert.Equal(0, session.CurrentScore);
        Assert.Equal(0, session.TotalScore);
    }

    [Fact]
    public void MixDefaultsToPhoenixAndConstructorPinsAnExplicitOne()
    {
        Assert.Equal(MixEnum.Phoenix, new TournamentSession(Guid.NewGuid(), Config()).Mix);
        Assert.Equal(MixEnum.Phoenix2,
            new TournamentSession(Guid.NewGuid(), Config(), MixEnum.Phoenix2).Mix);
        Assert.Equal(MixEnum.Phoenix2,
            new TournamentSession(Guid.NewGuid(), Config(), Array.Empty<TournamentSession.Entry>(),
                MixEnum.Phoenix2).Mix);
    }

    [Fact]
    public void CanAddReturnsFalseWhenScorelessScoreIsZero()
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());
        // SinglePerformance has a 0.0 ChartTypeModifier in the default ScoringConfiguration.
        var chart = new ChartBuilder().WithType(ChartType.SinglePerformance).Build();

        Assert.False(session.CanAdd(chart));
    }

    [Fact]
    public void CanAddAllowsAClosingChartThatOverhangsTheWindow()
    {
        // The window governs when a chart may start, not when it must finish (the Gargoyle
        // close): 90s entered of a 120s window leaves the window open, so a 90s closer is
        // legal even though it finishes past the buzzer.
        var config = Config();
        config.MaxTime = TimeSpan.FromMinutes(2);
        var session = new TournamentSession(Guid.NewGuid(), config);
        var first = new ChartBuilder().WithSong(SongOfDuration("song-a", TimeSpan.FromSeconds(90))).Build();
        var closer = new ChartBuilder().WithSong(SongOfDuration("song-b", TimeSpan.FromSeconds(90))).Build();
        session.Add(first, 900000, PhoenixPlate.SuperbGame, isBroken: false);

        Assert.True(session.CanAdd(closer));
    }

    [Fact]
    public void CanAddReturnsFalseOnceEnteredDurationsFillTheWindow()
    {
        // Every chart is provisionally the last; once the durations already entered reach
        // MaxTime the window is spent, and no candidate may start — not even a short one.
        var config = Config();
        config.MaxTime = TimeSpan.FromMinutes(2);
        var session = new TournamentSession(Guid.NewGuid(), config);
        session.Add(new ChartBuilder().WithSong(SongOfDuration("song-a", TimeSpan.FromSeconds(60))).Build(),
            900000, PhoenixPlate.SuperbGame, isBroken: false);
        session.Add(new ChartBuilder().WithSong(SongOfDuration("song-b", TimeSpan.FromSeconds(60))).Build(),
            900000, PhoenixPlate.SuperbGame, isBroken: false);
        var candidate = new ChartBuilder().WithSong(SongOfDuration("song-c", TimeSpan.FromSeconds(1))).Build();

        Assert.False(session.CanAdd(candidate));
    }

    [Fact]
    public void RestTimeFloorsAtZeroWhenTheClosingChartOverhangs()
    {
        var config = Config();
        config.MaxTime = TimeSpan.FromMinutes(2);
        var session = new TournamentSession(Guid.NewGuid(), config);
        session.Add(new ChartBuilder().WithSong(SongOfDuration("song-a", TimeSpan.FromSeconds(90))).Build(),
            900000, PhoenixPlate.SuperbGame, isBroken: false);
        session.Add(new ChartBuilder().WithSong(SongOfDuration("song-b", TimeSpan.FromSeconds(90))).Build(),
            900000, PhoenixPlate.SuperbGame, isBroken: false);

        Assert.Equal(TimeSpan.Zero, session.CurrentRestTime);
        Assert.Equal(TimeSpan.Zero, session.AverageTimeBetweenCharts);
    }

    [Fact]
    public void AverageTimeWithAddedChartFloorsAtZeroWhenTheCandidateOverhangs()
    {
        var config = Config();
        config.MaxTime = TimeSpan.FromMinutes(2);
        var session = new TournamentSession(Guid.NewGuid(), config);
        session.Add(new ChartBuilder().WithSong(SongOfDuration("song-a", TimeSpan.FromSeconds(90))).Build(),
            900000, PhoenixPlate.SuperbGame, isBroken: false);
        var closer = new ChartBuilder().WithSong(SongOfDuration("song-b", TimeSpan.FromSeconds(90))).Build();

        Assert.Equal(TimeSpan.Zero, session.AverageTimeWithAddedChart(closer));
    }

    [Fact]
    public void ARepeatedChartMayEnterAgainAndTheBetterPlayStays()
    {
        // D39: play a chart twice and only the better score counts; the other never enters.
        var config = Config();
        config.AllowRepeats = false;
        var session = new TournamentSession(Guid.NewGuid(), config);
        var first = new ChartBuilder().WithSongName("Repeat").WithLevel(15).WithType(ChartType.Single).Build();
        // Same song name, level, and type but different chart instance.
        var second = new ChartBuilder().WithSongName("Repeat").WithLevel(15).WithType(ChartType.Single).Build();
        session.Add(first, 900000, PhoenixPlate.SuperbGame, isBroken: false);

        Assert.True(session.CanAdd(second));
        Assert.Equal(TournamentSession.AddOutcome.Replaced,
            session.Add(second, 950000, PhoenixPlate.MarvelousGame, isBroken: false));
        var held = Assert.Single(session.Entries);
        Assert.Equal((PhoenixScore)950000, held.Score);
        Assert.Equal(PhoenixPlate.MarvelousGame, held.Plate);
        Assert.Equal((int)config.Scoring.GetScore(first, 950000, PhoenixPlate.MarvelousGame, false), held.SessionScore);

        Assert.Equal(TournamentSession.AddOutcome.KeptExisting,
            session.Add(first, 920000, PhoenixPlate.SuperbGame, isBroken: false));
        Assert.Equal((PhoenixScore)950000, Assert.Single(session.Entries).Score);
    }

    [Fact]
    public void ATieKeepsThePlayAlreadyHeld()
    {
        var config = Config();
        config.AllowRepeats = false;
        var session = new TournamentSession(Guid.NewGuid(), config);
        var chart = new ChartBuilder().WithSongName("Tie").WithLevel(15).WithType(ChartType.Single).Build();
        session.Add(chart, 900000, PhoenixPlate.SuperbGame, isBroken: false);

        Assert.Equal(TournamentSession.AddOutcome.KeptExisting,
            session.Add(chart, 900000, PhoenixPlate.MarvelousGame, isBroken: false));
        Assert.Equal(PhoenixPlate.SuperbGame, Assert.Single(session.Entries).Plate);
    }

    [Fact]
    public void ABetterPlayOfAHeldChartEntersEvenWhenTheWindowIsFull()
    {
        // A replacement adds no duration, so the full-window rule does not apply to it.
        var config = Config();
        config.AllowRepeats = false;
        config.MaxTime = TimeSpan.FromSeconds(90);
        var session = new TournamentSession(Guid.NewGuid(), config);
        var filler = new ChartBuilder().WithSong(SongOfDuration("filler", TimeSpan.FromSeconds(90))).Build();
        session.Add(filler, 900000, PhoenixPlate.SuperbGame, isBroken: false);
        var another = new ChartBuilder().WithSong(SongOfDuration("another", TimeSpan.FromSeconds(60))).Build();

        Assert.False(session.CanAdd(another));
        Assert.True(session.CanAdd(filler));
        Assert.Equal(TournamentSession.AddOutcome.Replaced,
            session.Add(filler, 960000, PhoenixPlate.SuperbGame, isBroken: false));
        Assert.Equal((PhoenixScore)960000, Assert.Single(session.Entries).Score);
    }

    [Fact]
    public void CanAddAllowsRepeatsWhenConfigEnablesThem()
    {
        var config = Config();
        config.AllowRepeats = true;
        var session = new TournamentSession(Guid.NewGuid(), config);
        var first = new ChartBuilder().WithSongName("Repeat").WithLevel(15).WithType(ChartType.Single).Build();
        var second = new ChartBuilder().WithSongName("Repeat").WithLevel(15).WithType(ChartType.Single).Build();
        session.Add(first, 900000, PhoenixPlate.SuperbGame, isBroken: false);

        Assert.True(session.CanAdd(second));
        // With repeats allowed a second play is its own entry, never a replacement.
        Assert.Equal(TournamentSession.AddOutcome.Added,
            session.Add(second, 950000, PhoenixPlate.SuperbGame, isBroken: false));
        Assert.Equal(2, session.Entries.Count);
    }

    [Fact]
    public void AddAppendsEntry()
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());
        var chart = new ChartBuilder().Build();

        session.Add(chart, 950000, PhoenixPlate.SuperbGame, isBroken: false);

        Assert.Single(session.Entries);
    }

    [Fact]
    public void AddThrowsArgumentExceptionForChartThatCannotBeAdded()
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());
        var invalid = new ChartBuilder().WithType(ChartType.SinglePerformance).Build();

        Assert.Throws<ArgumentException>(() =>
            session.Add(invalid, 900000, PhoenixPlate.SuperbGame, isBroken: false));
    }

    [Fact]
    public void SwapReplacesEntry()
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());
        var chart = new ChartBuilder().Build();
        session.Add(chart, 800000, PhoenixPlate.FairGame, isBroken: false);
        var original = session.Entries.Single();

        session.Swap(original, 990000, PhoenixPlate.PerfectGame, isBroken: false);

        var swapped = session.Entries.Single();
        Assert.Equal((PhoenixScore)990000, swapped.Score);
        Assert.Equal(PhoenixPlate.PerfectGame, swapped.Plate);
    }

    [Fact]
    public void SwapRecomputesBonusPointsForTheNewScore()
    {
        // BonusPoints is the chart-level-snapshot bump — the score with the balanced-level
        // override minus the score without it. It scales with the grade multiplier, so a
        // swapped entry keeping its old score's bonus is observable data.
        var chart = new ChartBuilder().WithLevel(20).Build();
        var config = Config();
        config.Scoring.ChartLevelSnapshot = new Dictionary<Guid, double> { [chart.Id] = 21.5 };
        var session = new TournamentSession(Guid.NewGuid(), config);
        session.Add(chart, 800000, PhoenixPlate.FairGame, isBroken: false);
        var original = session.Entries.Single();
        Assert.True(original.BonusPoints > 0);

        session.Swap(original, 990000, PhoenixPlate.PerfectGame, isBroken: false);

        var swapped = session.Entries.Single();
        var withBonus = config.Scoring.GetScore(chart, 990000, PhoenixPlate.PerfectGame, false);
        var basePoints = config.Scoring.GetScore(chart, 990000, PhoenixPlate.PerfectGame, false, false);
        Assert.Equal((int)withBonus, swapped.SessionScore);
        Assert.Equal((int)(withBonus - basePoints), swapped.BonusPoints);
        Assert.NotEqual(original.BonusPoints, swapped.BonusPoints);
    }

    [Fact]
    public void SwapIsNoOpWhenEntryNotInList()
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());
        var chart = new ChartBuilder().Build();
        var stranger = new TournamentSession.Entry(chart, 900000, PhoenixPlate.SuperbGame,
            IsBroken: false, SessionScore: 1, BonusPoints: 0);

        session.Swap(stranger, 1000000, PhoenixPlate.PerfectGame, isBroken: false);

        Assert.Empty(session.Entries);
    }

    [Fact]
    public void RemoveRemovesEntry()
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());
        session.Add(new ChartBuilder().Build(), 900000, PhoenixPlate.SuperbGame, isBroken: false);
        var entry = session.Entries.Single();

        session.Remove(entry);

        Assert.Empty(session.Entries);
    }

    [Fact]
    public void TotalScoreReflectsAddedEntriesWhenStartedEmpty()
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());
        Assert.Equal(0, session.TotalScore);

        session.Add(new ChartBuilder().WithSongName("a").Build(), 950000, PhoenixPlate.SuperbGame, isBroken: false);
        var afterFirst = session.TotalScore;
        session.Add(new ChartBuilder().WithSongName("b").Build(), 990000, PhoenixPlate.PerfectGame, isBroken: false);
        var afterSecond = session.TotalScore;

        Assert.True(afterFirst > 0);
        Assert.True(afterSecond > afterFirst);
        // CurrentScore is captured only by the entries-overload constructor — Add does not update it.
        Assert.Equal(0, session.CurrentScore);
    }

    [Fact]
    public void EntriesOverloadConstructorComputesCurrentScoreFromEntries()
    {
        var entries = new[]
        {
            new TournamentSession.Entry(new ChartBuilder().Build(), 900000, PhoenixPlate.SuperbGame,
                IsBroken: false, SessionScore: 100, BonusPoints: 0),
            new TournamentSession.Entry(new ChartBuilder().Build(), 950000, PhoenixPlate.SuperbGame,
                IsBroken: false, SessionScore: 250, BonusPoints: 0)
        };

        var session = new TournamentSession(Guid.NewGuid(), Config(), entries);

        Assert.Equal(350, session.CurrentScore);
        Assert.Equal(350, session.TotalScore);
    }

    private static TournamentConfiguration Config() =>
        new(new ScoringConfiguration());

    private static Song SongOfDuration(string name, TimeSpan duration) =>
        new(Name.From(name), SongType.Arcade, new Uri("https://example.invalid/song.png"),
            duration, Name.From("artist"), Bpm: null);
}
