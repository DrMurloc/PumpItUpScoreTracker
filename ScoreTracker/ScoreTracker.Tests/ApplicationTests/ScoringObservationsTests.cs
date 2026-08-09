using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ScoreTracker.OfficialMirror.Infrastructure;
using ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The detector's arithmetic, pinned before it goes anywhere near a real import. What the
///     telemetry is for is the IMPLIED constant on each line — if that number is wrong the whole
///     exercise reads back a wrong answer with total confidence, so it is asserted directly
///     against a row whose value is known to be mispriced today.
/// </summary>
public sealed class ScoringObservationsTests
{
    /// <summary>Captures what would have gone to the telemetry, rendered as the reader sees it.</summary>
    private sealed class CapturingLogger : ILogger
    {
        public CapturingLogger(bool enabled = true) => Enabled = enabled;

        private bool Enabled { get; }

        public List<string> Lines { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => Enabled;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));
    }

    private static PiuGameGetPumbilityResult.Entry Row(ChartType type, int level, PhoenixLetterGrade grade,
        PhoenixPlate plate, double value) =>
        new()
        {
            SongName = "Song",
            ChartType = type,
            Level = DifficultyLevel.From(level),
            Grade = grade,
            Plate = plate,
            Value = value
        };

    [Fact]
    public void PumbilityRowThatMatchesTheShippedTableStillLogsWithItsVerdict()
    {
        // Every row logs, not only the mismatches: a cell that never appears would otherwise be
        // indistinguishable from a cell that is correct, and four plate x type cells have never
        // been observed at all. Doubles S23 AA MG = Base(23) 245 x (1.36 + 0.006).
        var logger = new CapturingLogger();

        ScoringObservations.ObservePumbility(logger, MixEnum.Phoenix2,
            new[] { Row(ChartType.Double, 23, PhoenixLetterGrade.AA, PhoenixPlate.MarvelousGame, 334.67) });

        var line = Assert.Single(logger.Lines);
        Assert.Contains("verdict match", line);
        Assert.Contains("MG Double", line);
    }

    [Fact]
    public void PumbilityRowNamesTheImpliedPlateBonusWhenOursIsWrong()
    {
        // The real 2026-08-08 finding: singles Extreme Game pays 0.014, not the 0.012 we ship.
        // Curiosity Overdrive S20 SSS+ EG prices at 355.79 officially, and singles price one
        // level up, so the unit is Base(21) = 235 and 355.79 / 235 = 1.514 = 1.50 + 0.014.
        var logger = new CapturingLogger();

        ScoringObservations.ObservePumbility(logger, MixEnum.Phoenix2,
            new[] { Row(ChartType.Single, 20, PhoenixLetterGrade.SSSPlus, PhoenixPlate.ExtremeGame, 355.79) });

        var line = Assert.Single(logger.Lines);
        Assert.Contains("verdict MISMATCH", line);
        Assert.Contains("implied plate bonus 0.0140", line);
        Assert.Contains("ship 0.012", line);
    }

    [Fact]
    public void PumbilityRowsPricedAtZeroAreSkipped()
    {
        // Zero is how the page prices a broken, co-op or sub-10 chart. Dividing by a base we
        // never applied would invent a multiplier out of a row that carries no information.
        var logger = new CapturingLogger();

        ScoringObservations.ObservePumbility(logger, MixEnum.Phoenix2,
            new[] { Row(ChartType.Single, 9, PhoenixLetterGrade.SSSPlus, PhoenixPlate.UltimateGame, 0) });

        Assert.Empty(logger.Lines);
    }

    [Fact]
    public void NothingIsFormattedWhenTheLevelIsSwitchedOff()
    {
        // Fifty rows of formatting and arithmetic per import, thrown away. Asked once rather
        // than paid per row, and pinned here so the guard cannot be dropped later.
        var logger = new CapturingLogger(enabled: false);

        ScoringObservations.ObservePumbility(logger, MixEnum.Phoenix2,
            new[] { Row(ChartType.Single, 20, PhoenixLetterGrade.SSSPlus, PhoenixPlate.ExtremeGame, 355.79) });
        ScoringObservations.ObserveGrades(logger, MixEnum.Phoenix2, new[]
        {
            new PiuGameGetRecentScoresResult
            {
                SongName = "Song", Level = DifficultyLevel.From(21), ChartType = ChartType.Single,
                Score = PhoenixScore.From(448852), Grade = PhoenixLetterGrade.F, IsBroken = true
            }
        });

        Assert.Empty(logger.Lines);
    }

    [Fact]
    public void BrokenLowScoreIsObservedOnPhoenix2BecauseThatIsWhereTheFloorsAreGuesses()
    {
        var logger = new CapturingLogger();

        ScoringObservations.ObserveGrades(logger, MixEnum.Phoenix2, new[]
        {
            new PiuGameGetRecentScoresResult
            {
                SongName = "Song", Level = DifficultyLevel.From(21), ChartType = ChartType.Single,
                Score = PhoenixScore.From(448852), Grade = PhoenixLetterGrade.F, IsBroken = true
            }
        });

        var line = Assert.Single(logger.Lines);
        Assert.Contains("LowBandGrade", line);
        Assert.Contains("448852", line);
        Assert.Contains("broken=True", line);
    }

    [Fact]
    public void AnAgreeingScoreAboveTheUnverifiedBandIsNotWorthALine()
    {
        var logger = new CapturingLogger();

        ScoringObservations.ObserveGrades(logger, MixEnum.Phoenix2, new[]
        {
            new PiuGameGetRecentScoresResult
            {
                SongName = "Song", Level = DifficultyLevel.From(21), ChartType = ChartType.Single,
                // A clean AA on the Phoenix 2 table, well clear of the bands still being guessed
                // at — nothing to report.
                Score = PhoenixScore.From(930000), Grade = PhoenixLetterGrade.AA, IsBroken = false
            }
        });

        Assert.Empty(logger.Lines);
    }

    [Fact]
    public void AGradeThatContradictsOurTableIsReportedEvenAboveTheUnverifiedBand()
    {
        // The other half: a disagreement is worth a line wherever it lands, because it means a
        // cutoff moved under us. 930,000 is an AA for us; the site saying A+ would move a floor.
        var logger = new CapturingLogger();

        ScoringObservations.ObserveGrades(logger, MixEnum.Phoenix2, new[]
        {
            new PiuGameGetRecentScoresResult
            {
                SongName = "Song", Level = DifficultyLevel.From(21), ChartType = ChartType.Single,
                Score = PhoenixScore.From(930000), Grade = PhoenixLetterGrade.APlus, IsBroken = false
            }
        });

        var line = Assert.Single(logger.Lines);
        Assert.Contains("GradeDisagreement", line);
        Assert.Contains("MISMATCH", line);
    }
}
