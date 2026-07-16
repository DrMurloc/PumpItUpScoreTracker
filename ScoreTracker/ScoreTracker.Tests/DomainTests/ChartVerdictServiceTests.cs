using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class ChartVerdictServiceTests
{
    private static ChartVerdictInputs Inputs(
        TierListCategory? passTier = null,
        TierListCategory? scoreTier = null,
        IReadOnlyDictionary<ParagonLevel, double>? letters = null,
        IReadOnlyList<LevelAverage>? averages = null,
        IReadOnlyList<LevelPasses>? passes = null,
        IReadOnlyList<PhoenixPlate>? plates = null,
        int? medianClearScore = null,
        int scoresTracked = 0,
        int passCount = 0,
        IReadOnlyDictionary<Skill, double>? skills = null,
        double? tensionFraction = null,
        MixEnum currentMix = MixEnum.Phoenix,
        MixEnum debutMix = MixEnum.Phoenix,
        IReadOnlyList<MixLevel>? mixLevels = null)
    {
        // Averages and pass counts come from one population in production, so a level with
        // a score average has passers behind it unless a test says otherwise. Without this
        // every averages-only fixture would silently describe a curve nobody played.
        passes ??= (averages ?? Array.Empty<LevelAverage>())
            .Select(a => new LevelPasses(a.Level, PopulatedBucket))
            .ToArray();
        return new ChartVerdictInputs(passTier, scoreTier, letters, averages ?? Array.Empty<LevelAverage>(),
            passes, plates ?? Array.Empty<PhoenixPlate>(), medianClearScore,
            scoresTracked, passCount, skills ?? new Dictionary<Skill, double>(), tensionFraction, currentMix,
            debutMix, mixLevels ?? Array.Empty<MixLevel>());
    }

    /// <summary>Comfortably over <see cref="ChartVerdictService.YieldKneeMinimumPassesPerLevel" />.</summary>
    private const int PopulatedBucket = 40;

    [Fact]
    public void PassVsScoreFiresWhenEitherTierLeavesMedium()
    {
        var facets = ChartVerdictService.ComputeFacets(
            Inputs(passTier: TierListCategory.VeryHard, scoreTier: TierListCategory.Medium));

        var verdict = facets.OfType<PassVsScoreVerdict>().Single();
        Assert.Equal(TierListCategory.VeryHard, verdict.PassTier);
        Assert.Equal(TierListCategory.Medium, verdict.ScoreTier);
    }

    [Fact]
    public void PassVsScoreStaysSilentWhenBothMediumOrEitherUnrecorded()
    {
        Assert.Empty(ChartVerdictService
            .ComputeFacets(Inputs(passTier: TierListCategory.Medium, scoreTier: TierListCategory.Medium))
            .OfType<PassVsScoreVerdict>());
        Assert.Empty(ChartVerdictService
            .ComputeFacets(Inputs(passTier: TierListCategory.VeryHard, scoreTier: TierListCategory.Unrecorded))
            .OfType<PassVsScoreVerdict>());
        Assert.Empty(ChartVerdictService
            .ComputeFacets(Inputs(passTier: TierListCategory.VeryHard))
            .OfType<PassVsScoreVerdict>());
    }

    [Fact]
    public void LetterWallPicksTheSteepestAdjacentJump()
    {
        var facets = ChartVerdictService.ComputeFacets(Inputs(letters: new Dictionary<ParagonLevel, double>
        {
            [ParagonLevel.AA] = 0.2,
            [ParagonLevel.SS] = 0.4,
            [ParagonLevel.SSS] = 0.75
        }));

        var wall = facets.OfType<LetterWallVerdict>().Single();
        Assert.Equal(ParagonLevel.SSS, wall.WallGrade);
        Assert.Equal(0.35, wall.PercentileJump, 1e-9);
    }

    [Fact]
    public void LetterWallStaysSilentBelowTheJumpThreshold()
    {
        var facets = ChartVerdictService.ComputeFacets(Inputs(letters: new Dictionary<ParagonLevel, double>
        {
            [ParagonLevel.AA] = 0.3,
            [ParagonLevel.SS] = 0.5,
            [ParagonLevel.SSS] = 0.7
        }));

        Assert.Empty(facets.OfType<LetterWallVerdict>());
    }

    [Fact]
    public void YieldKneeFindsTheFirstLevelAveragingSsPlus()
    {
        var facets = ChartVerdictService.ComputeFacets(Inputs(averages: new[]
        {
            new LevelAverage(20, 940_000),
            new LevelAverage(21, 960_000),
            new LevelAverage(22, 978_000),
            new LevelAverage(23, 990_000)
        }));

        Assert.Equal(22, facets.OfType<YieldKneeVerdict>().Single().KneeLevel);
    }

    [Fact]
    public void OneStrayPasserFarBelowTheFolderIsNotWhereScoresOpenUp()
    {
        // A real S20 reported that scores open up at competitive level 12, on the strength
        // of a single passer there. The knee reads the FIRST level to cross, so the
        // thinnest bucket on the curve is exactly the one that gets believed — and one
        // player is enough to be a level's entire population. It also catches the unrated:
        // ~900 accounts sit at competitive level 1.0 or below, a "not enough data" floor
        // rather than a skill, and they scatter a passer or two across charts far above
        // themselves.
        var facets = ChartVerdictService.ComputeFacets(Inputs(
            averages: new[]
            {
                new LevelAverage(12, 985_000),
                new LevelAverage(20, 940_000),
                new LevelAverage(21, 960_000),
                new LevelAverage(22, 978_000)
            },
            passes: new[]
            {
                new LevelPasses(12, 1),
                new LevelPasses(20, 40),
                new LevelPasses(21, 55),
                new LevelPasses(22, 70)
            }));

        Assert.Equal(22, facets.OfType<YieldKneeVerdict>().Single().KneeLevel);
    }

    [Fact]
    public void AThinBucketCanDelayTheKneeButNeverInventOne()
    {
        // The guard only ever declines to believe a level — it cannot manufacture a
        // crossing that the populated curve never makes.
        var facets = ChartVerdictService.ComputeFacets(Inputs(
            averages: new[] { new LevelAverage(20, 940_000), new LevelAverage(21, 950_000) },
            passes: new[] { new LevelPasses(20, 40), new LevelPasses(21, 40) }));

        Assert.Empty(facets.OfType<YieldKneeVerdict>());
    }

    [Fact]
    public void YieldKneeStaysSilentWhenTheCurveStartsAboveOrNeverArrives()
    {
        Assert.Empty(ChartVerdictService.ComputeFacets(Inputs(averages: new[]
        {
            new LevelAverage(24, 980_000),
            new LevelAverage(25, 990_000)
        })).OfType<YieldKneeVerdict>());
        Assert.Empty(ChartVerdictService.ComputeFacets(Inputs(averages: new[]
        {
            new LevelAverage(20, 900_000),
            new LevelAverage(21, 930_000)
        })).OfType<YieldKneeVerdict>());
    }

    [Fact]
    public void PassBandReportsTheInterquartileLevels()
    {
        var facets = ChartVerdictService.ComputeFacets(Inputs(passes: new[]
        {
            new LevelPasses(20, 5),
            new LevelPasses(21, 10),
            new LevelPasses(22, 5)
        }));

        var band = facets.OfType<PassBandVerdict>().Single();
        Assert.Equal(20, band.LowerLevel);
        Assert.Equal(21, band.UpperLevel);
    }

    [Fact]
    public void PassBandStaysSilentUnderTwentyPasses()
    {
        var facets = ChartVerdictService.ComputeFacets(Inputs(passes: new[] { new LevelPasses(20, 19) }));

        Assert.Empty(facets.OfType<PassBandVerdict>());
    }

    [Fact]
    public void PlateResidualReportsStepsAgainstTheExpectedPlate()
    {
        // Median clear 975k predicts Marvelous Game; a Rough-Game median is three steps
        // below — the kill-spot signature, negative by convention.
        var facets = ChartVerdictService.ComputeFacets(Inputs(
            plates: Enumerable.Repeat(PhoenixPlate.RoughGame, 50).ToArray(),
            medianClearScore: 975_000));

        Assert.Equal(-3, facets.OfType<PlateResidualVerdict>().Single().StepsVsExpected);
    }

    [Fact]
    public void PlateResidualStaysSilentBelowFiftyClearsOrWithinOneStep()
    {
        Assert.Empty(ChartVerdictService.ComputeFacets(Inputs(
                plates: Enumerable.Repeat(PhoenixPlate.RoughGame, 49).ToArray(), medianClearScore: 975_000))
            .OfType<PlateResidualVerdict>());
        Assert.Empty(ChartVerdictService.ComputeFacets(Inputs(
                plates: Enumerable.Repeat(PhoenixPlate.MarvelousGame, 50).ToArray(), medianClearScore: 975_000))
            .OfType<PlateResidualVerdict>());
    }

    [Fact]
    public void StyleFingerprintTakesTheTopTwoQualifyingSkillsAndTheSustainedFlag()
    {
        var facets = ChartVerdictService.ComputeFacets(Inputs(
            skills: new Dictionary<Skill, double>
            {
                [Skill.Stamina] = 0.5,
                [Skill.EndRun] = 0.3,
                [Skill.VeryFast] = 0.1
            },
            tensionFraction: 0.7));

        var fingerprint = facets.OfType<StyleFingerprintVerdict>().Single();
        Assert.Equal(new[] { Skill.Stamina, Skill.EndRun }, fingerprint.TopSkills.Select(s => s.Skill).ToArray());
        Assert.True(fingerprint.IsSustained);
    }

    [Fact]
    public void StyleFingerprintStaysSilentWhenNoSkillClearsTheCoverageBar()
    {
        var facets = ChartVerdictService.ComputeFacets(Inputs(
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 0.2 }, tensionFraction: 0.9));

        Assert.Empty(facets.OfType<StyleFingerprintVerdict>());
    }

    [Fact]
    public void HistoryFiresOnAForeignDebutOrALevelChange()
    {
        var foreignDebut = ChartVerdictService.ComputeFacets(Inputs(debutMix: MixEnum.XX,
            mixLevels: new[] { new MixLevel(MixEnum.XX, 19), new MixLevel(MixEnum.Phoenix, 19) }));
        var verdict = foreignDebut.OfType<HistoryVerdict>().Single();
        Assert.Equal(MixEnum.XX, verdict.DebutMix);
        Assert.Equal(2, verdict.Levels.Count);

        var levelChange = ChartVerdictService.ComputeFacets(Inputs(
            mixLevels: new[] { new MixLevel(MixEnum.Phoenix, 19), new MixLevel(MixEnum.Phoenix2, 20) }));
        Assert.Single(levelChange.OfType<HistoryVerdict>());

        var unchanged = ChartVerdictService.ComputeFacets(Inputs(
            mixLevels: new[] { new MixLevel(MixEnum.Phoenix, 20) }));
        Assert.Empty(unchanged.OfType<HistoryVerdict>());
    }

    [Fact]
    public void PopulationIsAlwaysPresentAndAlwaysLast()
    {
        var sparse = ChartVerdictService.ComputeFacets(Inputs());
        var population = Assert.IsType<PopulationVerdict>(Assert.Single(sparse));
        Assert.Equal(0, population.ScoresTracked);
        Assert.Equal(0, population.PassRate);

        var counted = ChartVerdictService.ComputeFacets(Inputs(scoresTracked: 115, passCount: 71));
        var rate = counted.OfType<PopulationVerdict>().Single();
        Assert.Equal(115, rate.ScoresTracked);
        Assert.Equal(71 / 115.0, rate.PassRate, 1e-9);
    }

    [Fact]
    public void FacetsArriveInSalienceOrder()
    {
        var facets = ChartVerdictService.ComputeFacets(Inputs(
            passTier: TierListCategory.VeryHard,
            scoreTier: TierListCategory.Easy,
            letters: new Dictionary<ParagonLevel, double> { [ParagonLevel.SS] = 0.4, [ParagonLevel.SSS] = 0.8 },
            averages: new[] { new LevelAverage(20, 900_000), new LevelAverage(22, 980_000) },
            // Passes sit on the levels the averages describe — they are one population.
            passes: new[] { new LevelPasses(20, 25), new LevelPasses(22, 25) },
            plates: Enumerable.Repeat(PhoenixPlate.RoughGame, 50).ToArray(),
            medianClearScore: 975_000,
            scoresTracked: 115,
            passCount: 71,
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 0.5 },
            tensionFraction: 0.7,
            debutMix: MixEnum.XX,
            mixLevels: new[] { new MixLevel(MixEnum.XX, 19), new MixLevel(MixEnum.Phoenix, 20) }));

        Assert.Equal(new[]
        {
            typeof(PassVsScoreVerdict), typeof(LetterWallVerdict), typeof(YieldKneeVerdict),
            typeof(StyleFingerprintVerdict), typeof(PassBandVerdict), typeof(PlateResidualVerdict),
            typeof(HistoryVerdict), typeof(PopulationVerdict)
        }, facets.Select(f => f.GetType()).ToArray());
    }
}
