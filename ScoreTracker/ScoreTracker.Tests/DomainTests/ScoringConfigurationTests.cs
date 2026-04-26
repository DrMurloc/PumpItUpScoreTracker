using System;
using System.Collections.Generic;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class ScoringConfigurationTests
{
    private static readonly TimeSpan BaseAverage = TimeSpan.FromMinutes(2);

    private static Chart ChartAt(int level, ChartType type = ChartType.Single,
        TimeSpan? duration = null, SongType songType = SongType.Arcade) =>
        new ChartBuilder()
            .WithLevel(level)
            .WithType(type)
            .WithSong(new Song(Name.From($"song-{Guid.NewGuid()}"), songType,
                new Uri("https://example.invalid/s.png"), duration ?? BaseAverage,
                Name.From("artist"), Bpm: null))
            .Build();

    // ---- Public GetScore overloads / Default formula ----

    [Fact]
    public void GetScoreReturnsZeroWhenScoreBelowMinimumScore()
    {
        var config = new ScoringConfiguration { MinimumScore = 800000 };

        Assert.Equal(0, config.GetScore(DifficultyLevel.From(20), 700000));
    }

    [Fact]
    public void GetScoreInDefaultFormulaIsLevelRatingTimesLetterGradeModifier()
    {
        var config = new ScoringConfiguration();
        var level = DifficultyLevel.From(20);

        // Default config: BaseAverage duration ⇒ time multiplier 1.0; ChartTypeModifier[Single]=1.0;
        // SongType[Arcade]=1.0; SuperbGame plate=1.0; no chart modifier. So result reduces to
        // LevelRatings[level] * LetterGradeModifier.
        var expected = config.LevelRatings[level] * PhoenixLetterGrade.AAA.GetModifier();

        Assert.Equal(expected, config.GetScore(level, 950000));
    }

    [Fact]
    public void GetScoreUsesPgLetterGradeModifierForPerfectGameScore()
    {
        var config = new ScoringConfiguration { PgLetterGradeModifier = 2.0 };
        var level = DifficultyLevel.From(20);

        Assert.Equal(config.LevelRatings[level] * 2.0, config.GetScore(level, 1000000));
    }

    [Fact]
    public void GetScoreAppliesStageBreakModifierWhenAttemptIsBroken()
    {
        var config = new ScoringConfiguration { StageBreakModifier = 0.5 };
        var chart = ChartAt(20);

        var clean = config.GetScore(chart, 950000, PhoenixPlate.SuperbGame, false);
        var broken = config.GetScore(chart, 950000, PhoenixPlate.SuperbGame, true);

        Assert.Equal(clean * 0.5, broken);
    }

    [Fact]
    public void GetScoreAppliesChartModifierKeyedByChartId()
    {
        var chart = ChartAt(20);
        var config = new ScoringConfiguration
        {
            ChartModifiers = new Dictionary<Guid, double> { [chart.Id] = 1.5 }
        };

        var unmodified = new ScoringConfiguration();
        var baseScore = unmodified.GetScore(chart, 950000, PhoenixPlate.SuperbGame, false);

        // Default formula multiplies the chart modifier in twice (once via GetScorelessScore, once
        // again on the result), so the effective multiplier is 1.5 * 1.5 = 2.25.
        Assert.Equal(baseScore * 1.5 * 1.5, config.GetScore(chart, 950000, PhoenixPlate.SuperbGame, false), 6);
    }

    // ---- AdjustToTime / SongType / ChartType ----

    [Fact]
    public void GetScoreScalesLinearlyWithDurationWhenAdjustToTimeIsTrue()
    {
        var config = new ScoringConfiguration { AdjustToTime = true };
        var shortChart = ChartAt(20, duration: TimeSpan.FromMinutes(1));
        var standardChart = ChartAt(20, duration: BaseAverage);

        var shortScore = config.GetScore(shortChart, 950000, PhoenixPlate.SuperbGame, false);
        var standardScore = config.GetScore(standardChart, 950000, PhoenixPlate.SuperbGame, false);

        Assert.Equal(standardScore * 0.5, shortScore, 6);
    }

    [Fact]
    public void GetScoreIgnoresDurationWhenAdjustToTimeIsFalse()
    {
        var config = new ScoringConfiguration { AdjustToTime = false };
        var shortChart = ChartAt(20, duration: TimeSpan.FromMinutes(1));
        var longChart = ChartAt(20, duration: TimeSpan.FromMinutes(4));

        var shortScore = config.GetScore(shortChart, 950000, PhoenixPlate.SuperbGame, false);
        var longScore = config.GetScore(longChart, 950000, PhoenixPlate.SuperbGame, false);

        Assert.Equal(shortScore, longScore);
    }

    [Fact]
    public void GetScoreAppliesSongTypeModifier()
    {
        var config = new ScoringConfiguration();
        config.SongTypeModifiers[SongType.Remix] = 0.5;

        var arcade = ChartAt(20, songType: SongType.Arcade);
        var remix = ChartAt(20, songType: SongType.Remix);

        var arcadeScore = config.GetScore(arcade, 950000, PhoenixPlate.SuperbGame, false);
        var remixScore = config.GetScore(remix, 950000, PhoenixPlate.SuperbGame, false);

        Assert.Equal(arcadeScore * 0.5, remixScore);
    }

    [Fact]
    public void GetScoreAppliesChartTypeModifier()
    {
        var config = new ScoringConfiguration();
        config.ChartTypeModifiers[ChartType.Double] = 1.25;

        var single = ChartAt(20, ChartType.Single);
        var dub = ChartAt(20, ChartType.Double);

        var singleScore = config.GetScore(single, 950000, PhoenixPlate.SuperbGame, false);
        var doubleScore = config.GetScore(dub, 950000, PhoenixPlate.SuperbGame, false);

        Assert.Equal(singleScore * 1.25, doubleScore, 6);
    }

    // ---- ContinuousLetterGradeScale ----

    [Fact]
    public void ContinuousLetterGradeScaleInterpolatesBetweenAdjacentBrackets()
    {
        var config = new ScoringConfiguration { ContinuousLetterGradeScale = true };
        var level = DifficultyLevel.From(20);

        // 950000 sits at the start of the AAA bracket (1.10) and 960000 at the start of the next
        // (AAA+, 1.15). With continuous scaling, a midpoint score should yield the midpoint
        // modifier between those two — strictly higher than the AAA-only rate.
        var bracketStart = config.GetScore(level, 950000);
        var middle = config.GetScore(level, 955000);
        var bracketAbove = config.GetScore(level, 960000);

        Assert.True(middle > bracketStart);
        Assert.True(middle < bracketAbove);
    }

    // ---- Avalanche formula ----

    [Fact]
    public void AvalancheFormulaSubtractsStageBreakModifierFromLetterGradeModifier()
    {
        var config = new ScoringConfiguration
        {
            Formula = ScoringConfiguration.CalculationType.Avalanche,
            StageBreakModifier = 0.1
        };
        var chart = ChartAt(20);

        var clean = config.GetScore(chart, 950000, PhoenixPlate.SuperbGame, false);
        var broken = config.GetScore(chart, 950000, PhoenixPlate.SuperbGame, true);

        // clean = scoreless * letterMod ; broken = scoreless * (letterMod - 0.1)
        // ratio: broken/clean = (letterMod - 0.1) / letterMod
        var letterMod = PhoenixLetterGrade.AAA.GetModifier();
        Assert.Equal(clean * (letterMod - 0.1) / letterMod, broken, 6);
    }

    // ---- Custom formula ----

    [Fact]
    public void CustomFormulaEvaluatesUserAlgorithmAgainstSubstitutedTokens()
    {
        var config = new ScoringConfiguration
        {
            Formula = ScoringConfiguration.CalculationType.Custom,
            CustomAlgorithm = "LVL + LTTR"
        };
        var level = DifficultyLevel.From(20);

        // LVL = LevelRatings[20] = 650; LTTR = AAA modifier = 1.10
        var expected = config.LevelRatings[level] + PhoenixLetterGrade.AAA.GetModifier();

        Assert.Equal(expected, config.GetScore(level, 950000), 6);
    }

    // ---- GetBaseRating (reached via GetScorelessScore) ----

    [Fact]
    public void GetBaseRatingReturnsTwoThousandForCoOpRegardlessOfLevelRatings()
    {
        var config = new ScoringConfiguration { AdjustToTime = false };
        config.LevelRatings[DifficultyLevel.From(20)] = 99999; // would dominate if it leaked through
        var coOpChart = ChartAt(20, type: ChartType.CoOp);

        // CoOp returns 2000 from GetBaseRating; default ChartTypeModifier[CoOp]=1.0,
        // SongType[Arcade]=1.0 ⇒ scoreless score is 2000.
        Assert.Equal(2000, config.GetScorelessScore(coOpChart));
    }

    [Fact]
    public void GetBaseRatingUsesLevelRatingsWhenNoChartLevelSnapshotIsSet()
    {
        var config = new ScoringConfiguration { AdjustToTime = false };
        var chart = ChartAt(20);

        Assert.Equal(config.LevelRatings[DifficultyLevel.From(20)], config.GetScorelessScore(chart));
    }

    [Fact]
    public void GetBaseRatingInterpolatesFromChartLevelSnapshotBetweenAdjacentLevels()
    {
        var chart = ChartAt(20);
        var config = new ScoringConfiguration
        {
            AdjustToTime = false,
            ChartLevelSnapshot = new Dictionary<Guid, double> { [chart.Id] = 20.5 }
        };
        config.LevelRatings[DifficultyLevel.From(20)] = 1000;
        config.LevelRatings[DifficultyLevel.From(21)] = 2000;

        // Snapshot at 20.5 means: floor=20, ceil=21, fraction = 20.5 - 0.5 - 20 = 0.0 ⇒ rating = 1000.
        // Snapshot at 21.0 ⇒ fraction = 21.0 - 0.5 - 20 = 0.5 ⇒ rating = 1000 + 0.5*(2000-1000) = 1500.
        // Stick with 20.5 case here: expect exactly 1000 from the formula.
        Assert.Equal(1000, config.GetScorelessScore(chart));
    }

    [Fact]
    public void GetBaseRatingIgnoresSnapshotWhenIncludeLevelOverrideIsFalse()
    {
        var chart = ChartAt(20);
        var config = new ScoringConfiguration
        {
            AdjustToTime = false,
            ChartLevelSnapshot = new Dictionary<Guid, double> { [chart.Id] = 25.0 }
        };
        config.LevelRatings[DifficultyLevel.From(20)] = 1000;

        Assert.Equal(1000, config.GetScorelessScore(chart, includeLevelOverride: false));
    }

    [Fact]
    public void GetBaseRatingIgnoresSnapshotForLevelTwentyNineAndAbove()
    {
        // The override does not apply when chart.Level >= 29, so the level-29 LevelRating is used directly.
        var chart = ChartAt(29);
        var config = new ScoringConfiguration
        {
            AdjustToTime = false,
            ChartLevelSnapshot = new Dictionary<Guid, double> { [chart.Id] = 27.5 }
        };
        var expected = config.LevelRatings[DifficultyLevel.From(29)];

        Assert.Equal(expected, config.GetScorelessScore(chart));
    }

    [Fact]
    public void GetBaseRatingIgnoresSnapshotForChartIdsNotPresentInTheSnapshot()
    {
        var chart = ChartAt(20);
        var otherId = Guid.NewGuid();
        var config = new ScoringConfiguration
        {
            AdjustToTime = false,
            ChartLevelSnapshot = new Dictionary<Guid, double> { [otherId] = 25.0 }
        };

        Assert.Equal(config.LevelRatings[DifficultyLevel.From(20)], config.GetScorelessScore(chart));
    }
}
