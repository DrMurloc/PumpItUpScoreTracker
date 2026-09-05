using System;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services.Theming;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The nine color systems' cutoffs and the glow rule live in one place. What is pinned here is
///     each system's shape at its edges and the three glow facts the owner ruled on: one strength,
///     Off switches off Perfect Games, a Perfect Game is inside any top-N rule.
/// </summary>
public sealed class ScoreStyleTests
{
    /// <summary>A hundred-strong cohort with <paramref name="better" /> peers above you.</summary>
    private static PeerStanding Standing(int better) =>
        new(99, 99, better, 0, 0, Array.Empty<PeerStandingSource>(), null);

    private static ScoreColorSettings With(ScoreColorSystem system) =>
        new(system, GlowRule.Off, 1);

    [Theory]
    [InlineData(95, "var(--rarity-common)")]
    [InlineData(60, "var(--rarity-silver)")]
    [InlineData(30, "var(--rarity-emerald)")]
    [InlineData(15, "var(--rarity-gold)")]
    [InlineData(5, "var(--rarity-sapphire)")]
    [InlineData(0, "var(--rarity-prism)")]
    public void TheJudgementSpectrumIsTheRarityRamp(int better, string token)
    {
        var style = ThemeScales.ScoreStyleFor(Standing(better), false, null, With(ScoreColorSystem.JudgementSpectrum));

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
        var style = ThemeScales.ScoreStyleFor(Standing(better), false, null, With(ScoreColorSystem.Classic));

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
        var style = ThemeScales.ScoreStyleFor(Standing(better), false, null, With(ScoreColorSystem.GradeMetals));

        Assert.Contains(token, style.Style);
    }

    [Theory]
    [InlineData(0, "--plate-sg")]
    [InlineData(1, "--plate-mg")]
    [InlineData(2, "--plate-fg")]
    public void ThePodiumPaintsThreePlaces(int better, string token)
    {
        var style = ThemeScales.ScoreStyleFor(Standing(better), false, null, With(ScoreColorSystem.Podium));

        Assert.Contains(token, style.Style);
    }

    [Fact]
    public void OffThePodiumIsPlainInk()
    {
        Assert.Equal(string.Empty,
            ThemeScales.ScoreStyleFor(Standing(3), false, null, With(ScoreColorSystem.Podium)).Style);
    }

    [Theory]
    [InlineData(95, "--judg-miss")]
    [InlineData(60, "--judg-bad")]
    [InlineData(30, "--judg-good")]
    [InlineData(15, "--judg-great")]
    [InlineData(0, "--judg-perfect")]
    public void TheResultScreenIsTheJudgementColorsLiterally(int better, string token)
    {
        var style = ThemeScales.ScoreStyleFor(Standing(better), false, null, With(ScoreColorSystem.ResultScreen));

        Assert.Contains(token, style.Style);
    }

    [Theory]
    [InlineData(60, "")]
    [InlineData(30, "color:var(--rarity-gold);")]
    [InlineData(5, "color:var(--rarity-sapphire);")]
    public void ThreeStepsIsPlainGoldThenIce(int better, string expected)
    {
        Assert.Equal(expected,
            ThemeScales.ScoreStyleFor(Standing(better), false, null, With(ScoreColorSystem.ThreeSteps)).Style);
    }

    [Fact]
    public void SingleHueClimbsSixLightnessSteps()
    {
        Assert.Contains("--hue-1", ThemeScales.ScoreStyleFor(Standing(95), false, null, With(ScoreColorSystem.SingleHue)).Style);
        Assert.Contains("--hue-6", ThemeScales.ScoreStyleFor(Standing(0), false, null, With(ScoreColorSystem.SingleHue)).Style);
    }

    [Fact]
    public void AStandingSystemPaintsNothingWhenNoPeerHasPassedTheChart()
    {
        var none = PeerStanding.NoCohort(12, 0, Array.Empty<PeerStandingSource>());

        Assert.Equal(string.Empty,
            ThemeScales.ScoreStyleFor(none, false, PhoenixLetterGrade.SSS, With(ScoreColorSystem.JudgementSpectrum)).Style);
        Assert.Equal(string.Empty,
            ThemeScales.ScoreStyleFor(null, false, PhoenixLetterGrade.SSS, With(ScoreColorSystem.Podium)).Style);
    }

    [Fact]
    public void TheActualGradeIgnoresTheStandingAndNoneIgnoresEverything()
    {
        Assert.Equal("color:var(--plate-ug);",
            ThemeScales.ScoreStyleFor(null, false, PhoenixLetterGrade.SSS, With(ScoreColorSystem.ActualGrade)).Style);
        Assert.Equal(string.Empty,
            ThemeScales.ScoreStyleFor(Standing(0), true, PhoenixLetterGrade.SSSPlus, With(ScoreColorSystem.None)).Style);
    }

    [Fact]
    public void OffSwitchesOffThePerfectGameGlowToo()
    {
        var style = ThemeScales.ScoreStyleFor(Standing(0), true, PhoenixLetterGrade.SSSPlus,
            new ScoreColorSettings(ScoreColorSystem.JudgementSpectrum, GlowRule.Off, 10));

        Assert.Equal(string.Empty, style.GlowClass);
    }

    [Fact]
    public void PerfectGamesOnlyLightsAPerfectGameAndNothingElse()
    {
        var settings = new ScoreColorSettings(ScoreColorSystem.JudgementSpectrum, GlowRule.PerfectGames, 10);

        Assert.Equal(ThemeScales.ScoreGlowClass, ThemeScales.ScoreStyleFor(null, true, null, settings).GlowClass);
        Assert.Equal(string.Empty, ThemeScales.ScoreStyleFor(Standing(0), false, null, settings).GlowClass);
    }

    [Fact]
    public void TopPlacesLightsUpToTheThresholdAndAPerfectGameAlways()
    {
        var settings = new ScoreColorSettings(ScoreColorSystem.JudgementSpectrum, GlowRule.TopPlaces, 3);

        Assert.Equal(ThemeScales.ScoreGlowClass, ThemeScales.ScoreStyleFor(Standing(2), false, null, settings).GlowClass);
        Assert.Equal(string.Empty, ThemeScales.ScoreStyleFor(Standing(3), false, null, settings).GlowClass);
        // No peer has passed it, but a million is inside any top three.
        Assert.Equal(ThemeScales.ScoreGlowClass, ThemeScales.ScoreStyleFor(null, true, null, settings).GlowClass);
    }

    [Fact]
    public void TopPercentLightsFromTheThresholdInclusiveWithOneStrength()
    {
        var settings = new ScoreColorSettings(ScoreColorSystem.JudgementSpectrum, GlowRule.TopPercent, 10);

        // 90 of 100 at or below you is exactly the top 10%.
        Assert.Equal(ThemeScales.ScoreGlowClass, ThemeScales.ScoreStyleFor(Standing(10), false, null, settings).GlowClass);
        Assert.Equal(string.Empty, ThemeScales.ScoreStyleFor(Standing(11), false, null, settings).GlowClass);
        Assert.Equal(ThemeScales.ScoreGlowClass, ThemeScales.ScoreStyleFor(Standing(0), false, null, settings).GlowClass);
    }
}
