using System;
using System.Globalization;
using System.Text.RegularExpressions;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services.Theming;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The nine color systems' cutoffs and the glow rules live in one place. What is pinned here is
///     each system's shape at its edges, the glow facts the owner ruled on — one strength for the peer
///     rules, Off switches off Perfect Games, a Perfect Game is inside any top-N rule — and the two
///     grade rules: strictly under N points, the last N% inclusive at its edge, no peers needed, and
///     Brighter the closer growing with closeness.
/// </summary>
public sealed class ScoreStyleTests
{
    /// <summary>A hundred-strong cohort with <paramref name="better" /> peers above you.</summary>
    private static PeerStanding Standing(int better) =>
        new(99, 99, better, 0, 0, Array.Empty<PeerStandingSource>(), null);

    private static GradeProgress Progress(int score) =>
        GradeProgress.Of(PhoenixScore.From(score), MixEnum.Phoenix2);

    private static ScoreColorSettings With(ScoreColorSystem system) =>
        new(system, GlowRule.Off, 1);

    private static ScoreColorSettings Glow(GlowRule rule, int threshold,
        GlowStrength strength = GlowStrength.One) =>
        new(ScoreColorSystem.JudgementSpectrum, rule, threshold, strength);

    [Theory]
    [InlineData(95, "var(--rarity-common)")]
    [InlineData(60, "var(--rarity-silver)")]
    [InlineData(30, "var(--rarity-emerald)")]
    [InlineData(15, "var(--rarity-gold)")]
    [InlineData(5, "var(--rarity-sapphire)")]
    [InlineData(0, "var(--rarity-prism)")]
    public void TheJudgementSpectrumIsTheRarityRamp(int better, string token)
    {
        var style = ThemeScales.ScoreStyleFor(Standing(better), null, With(ScoreColorSystem.JudgementSpectrum));

        Assert.Equal($"color:{token};", style.Style);
    }

    [Theory]
    [InlineData(95, "--classic-1")]
    [InlineData(80, "--classic-2")]
    [InlineData(60, "--classic-3")]
    [InlineData(30, "--classic-4")]
    [InlineData(15, "--classic-5")]
    [InlineData(5, "--classic-6")]
    [InlineData(0, "--classic-7")]
    public void TheClassicLadderHasSevenRungsWithPinkOnTop(int better, string token)
    {
        var style = ThemeScales.ScoreStyleFor(Standing(better), null, With(ScoreColorSystem.Classic));

        Assert.Contains(token, style.Style);
    }

    [Theory]
    [InlineData(95, "--grade-sub-a")]
    [InlineData(60, "--plate-fg")]
    [InlineData(30, "--plate-mg")]
    [InlineData(15, "--plate-eg")]
    [InlineData(5, "--plate-ug")]
    [InlineData(0, "--plate-pg")]
    public void TheGradeMetalsClimbFromBelowAGreenToTheSssPlusIce(int better, string token)
    {
        var style = ThemeScales.ScoreStyleFor(Standing(better), null, With(ScoreColorSystem.GradeMetals));

        Assert.Contains(token, style.Style);
    }

    [Theory]
    [InlineData(0, "--plate-sg")]
    [InlineData(1, "--plate-mg")]
    [InlineData(2, "--plate-fg")]
    public void ThePodiumPaintsThreePlaces(int better, string token)
    {
        var style = ThemeScales.ScoreStyleFor(Standing(better), null, With(ScoreColorSystem.Podium));

        Assert.Contains(token, style.Style);
    }

    [Fact]
    public void OffThePodiumIsPlainInk()
    {
        Assert.Equal(string.Empty,
            ThemeScales.ScoreStyleFor(Standing(3), null, With(ScoreColorSystem.Podium)).Style);
    }

    [Theory]
    [InlineData(95, "--judg-miss")]
    [InlineData(60, "--judg-bad")]
    [InlineData(30, "--judg-good")]
    [InlineData(15, "--judg-great")]
    [InlineData(0, "--judg-perfect")]
    public void TheResultScreenIsTheJudgementColorsLiterally(int better, string token)
    {
        var style = ThemeScales.ScoreStyleFor(Standing(better), null, With(ScoreColorSystem.ResultScreen));

        Assert.Contains(token, style.Style);
    }

    [Theory]
    [InlineData(60, "")]
    [InlineData(30, "color:var(--rarity-gold);")]
    [InlineData(5, "color:var(--rarity-sapphire);")]
    public void ThreeStepsIsPlainGoldThenIce(int better, string expected)
    {
        Assert.Equal(expected,
            ThemeScales.ScoreStyleFor(Standing(better), null, With(ScoreColorSystem.ThreeSteps)).Style);
    }

    [Fact]
    public void SingleHueClimbsSixLightnessSteps()
    {
        Assert.Contains("--hue-1", ThemeScales.ScoreStyleFor(Standing(95), null, With(ScoreColorSystem.SingleHue)).Style);
        Assert.Contains("--hue-6", ThemeScales.ScoreStyleFor(Standing(0), null, With(ScoreColorSystem.SingleHue)).Style);
    }

    [Fact]
    public void AStandingSystemPaintsNothingWhenNoPeerHasPassedTheChart()
    {
        var none = PeerStanding.NoCohort(12, 0, Array.Empty<PeerStandingSource>());

        Assert.Equal(string.Empty,
            ThemeScales.ScoreStyleFor(none, Progress(992_000), With(ScoreColorSystem.JudgementSpectrum)).Style);
        Assert.Equal(string.Empty,
            ThemeScales.ScoreStyleFor(null, Progress(992_000), With(ScoreColorSystem.Podium)).Style);
    }

    [Fact]
    public void TheActualGradeIgnoresTheStandingAndNoneIgnoresEverything()
    {
        Assert.Equal("color:var(--plate-ug);",
            ThemeScales.ScoreStyleFor(null, Progress(992_000), With(ScoreColorSystem.ActualGrade)).Style);
        Assert.Equal(string.Empty,
            ThemeScales.ScoreStyleFor(Standing(0), Progress(1_000_000), With(ScoreColorSystem.None)).Style);
    }

    [Fact]
    public void OffSwitchesOffThePerfectGameGlowToo()
    {
        var style = ThemeScales.ScoreStyleFor(Standing(0), Progress(1_000_000),
            new ScoreColorSettings(ScoreColorSystem.JudgementSpectrum, GlowRule.Off, 10));

        Assert.Equal(string.Empty, style.GlowClass);
    }

    [Fact]
    public void PerfectGamesOnlyLightsAPerfectGameAndNothingElse()
    {
        var settings = new ScoreColorSettings(ScoreColorSystem.JudgementSpectrum, GlowRule.PerfectGames, 10);

        Assert.Equal(ThemeScales.ScoreGlowClass, ThemeScales.ScoreStyleFor(null, Progress(1_000_000), settings).GlowClass);
        Assert.Equal(string.Empty, ThemeScales.ScoreStyleFor(Standing(0), Progress(992_000), settings).GlowClass);
    }

    [Fact]
    public void TopPlacesLightsUpToTheThresholdAndAPerfectGameAlways()
    {
        var settings = new ScoreColorSettings(ScoreColorSystem.JudgementSpectrum, GlowRule.TopPlaces, 3);

        Assert.Equal(ThemeScales.ScoreGlowClass, ThemeScales.ScoreStyleFor(Standing(2), null, settings).GlowClass);
        Assert.Equal(string.Empty, ThemeScales.ScoreStyleFor(Standing(3), null, settings).GlowClass);
        // No peer has passed it, but a million is inside any top three.
        Assert.Equal(ThemeScales.ScoreGlowClass, ThemeScales.ScoreStyleFor(null, Progress(1_000_000), settings).GlowClass);
    }

    [Fact]
    public void TopPercentLightsFromTheThresholdInclusiveWithOneStrength()
    {
        var settings = new ScoreColorSettings(ScoreColorSystem.JudgementSpectrum, GlowRule.TopPercent, 10);

        // 90 of 100 at or below you is exactly the top 10%.
        Assert.Equal(ThemeScales.ScoreGlowClass, ThemeScales.ScoreStyleFor(Standing(10), null, settings).GlowClass);
        Assert.Equal(string.Empty, ThemeScales.ScoreStyleFor(Standing(11), null, settings).GlowClass);
        Assert.Equal(ThemeScales.ScoreGlowClass, ThemeScales.ScoreStyleFor(Standing(0), null, settings).GlowClass);
    }

    [Fact]
    public void APeerRuleKeepsOneStrengthWhateverTheGradeRulesWereSetTo()
    {
        var settings = Glow(GlowRule.TopPercent, 10, GlowStrength.BrighterTheCloser);

        var style = ThemeScales.ScoreStyleFor(Standing(0), Progress(994_850), settings);

        Assert.Equal(ThemeScales.ScoreGlowClass, style.GlowClass);
        Assert.Equal(string.Empty, style.GlowStyle);
    }

    [Theory]
    [InlineData(989_050, true)] // 950 short of SSS
    [InlineData(999_420, true)] // 580 short of a Perfect Game
    [InlineData(989_000, false)] // exactly 1,000 short: under means under
    [InlineData(978_700, false)] // 1,300 short of SS
    public void UnderPointsLightsAScoreStrictlyUnderTheThresholdFromItsNextGrade(int score, bool lit)
    {
        var style = ThemeScales.ScoreStyleFor(null, Progress(score), Glow(GlowRule.UnderPointsToNextGrade, 1000));

        Assert.Equal(lit ? ThemeScales.ScoreGlowClass : string.Empty, style.GlowClass);
    }

    [Theory]
    [InlineData(974_000, true)] // exactly four fifths through an S: the window's edge is inside it
    [InlineData(973_999, false)]
    [InlineData(968_900, true)] // 1,100 short of S, but 89% through a 10,000-wide AAA+
    [InlineData(884_000, true)] // 16,000 short of A+, 84% through a 100,000-wide Phoenix 2 A
    [InlineData(987_300, false)] // 46% through SS+
    public void LastPercentLightsTheSameShareOfEveryGrade(int score, bool lit)
    {
        var style = ThemeScales.ScoreStyleFor(null, Progress(score), Glow(GlowRule.LastPercentOfGrade, 20));

        Assert.Equal(lit ? ThemeScales.ScoreGlowClass : string.Empty, style.GlowClass);
    }

    [Fact]
    public void AHundredPercentLightsAScoreSittingOnItsGradesFloor()
    {
        var style = ThemeScales.ScoreStyleFor(null, Progress(970_000), Glow(GlowRule.LastPercentOfGrade, 100));

        Assert.Equal(ThemeScales.ScoreGlowClass, style.GlowClass);
    }

    [Theory]
    [InlineData(GlowRule.UnderPointsToNextGrade, 500)]
    [InlineData(GlowRule.LastPercentOfGrade, 10)]
    public void APerfectGameGlowsUnderBothGradeRules(GlowRule rule, int threshold)
    {
        var style = ThemeScales.ScoreStyleFor(null, Progress(1_000_000), Glow(rule, threshold));

        Assert.Equal(ThemeScales.ScoreGlowClass, style.GlowClass);
    }

    [Fact]
    public void AGradeRuleNeedsNoPeersAndLeavesTheColorToTheStanding()
    {
        var unmeasured = ThemeScales.ScoreStyleFor(null, Progress(989_050), Glow(GlowRule.UnderPointsToNextGrade, 1000));

        Assert.Equal(string.Empty, unmeasured.Style);
        Assert.Equal(ThemeScales.ScoreGlowClass, unmeasured.GlowClass);
    }

    [Fact]
    public void BrighterTheCloserStrengthensAsTheScoreNearsTheNextGrade()
    {
        var settings = Glow(GlowRule.LastPercentOfGrade, 100, GlowStrength.BrighterTheCloser);

        var far = ThemeScales.ScoreStyleFor(Standing(5), Progress(970_800), settings);
        var near = ThemeScales.ScoreStyleFor(Standing(5), Progress(974_800), settings);
        var perfect = ThemeScales.ScoreStyleFor(Standing(5), Progress(1_000_000), settings);

        Assert.Equal(ThemeScales.ScoreGlowCloserClass, far.GlowClass);
        Assert.StartsWith("text-shadow:", far.GlowStyle);
        Assert.True(OuterBlur(far) < OuterBlur(near));
        Assert.True(OuterBlur(near) <= OuterBlur(perfect));
        // The color stays its own fragment: a surface borrowing the color never borrows the glow.
        Assert.DoesNotContain("text-shadow", near.Style);
    }

    [Fact]
    public void BrighterTheCloserWritesCssNumbersInEveryCulture()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            var style = ThemeScales.ScoreStyleFor(null, Progress(974_400),
                Glow(GlowRule.UnderPointsToNextGrade, 1000, GlowStrength.BrighterTheCloser));

            Assert.DoesNotContain(",5px", style.GlowStyle);
            Assert.Matches(@"^text-shadow:0 0 \d+\.\dpx color-mix", style.GlowStyle);
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    /// <summary>The outer halo's blur radius, the part of the glow that grows the most.</summary>
    private static double OuterBlur(ScoreStyle style)
    {
        var blurs = Regex.Matches(style.GlowStyle, @"0 0 (\d+\.\d)px");
        return double.Parse(blurs[^1].Groups[1].Value, CultureInfo.InvariantCulture);
    }
}
