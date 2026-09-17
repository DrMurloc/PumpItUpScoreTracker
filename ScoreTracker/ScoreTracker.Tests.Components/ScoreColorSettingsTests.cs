using ScoreTracker.Web.Services.Theming;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class ScoreColorSettingsTests
{
    [Fact]
    public void ANeverSavedSettingIsTheJudgementSpectrumGlowingFromTheTopTenPercent()
    {
        var settings = ScoreColorSettings.Parse(null);

        Assert.Equal(ScoreColorSystem.JudgementSpectrum, settings.System);
        Assert.Equal(GlowRule.TopPercent, settings.Glow);
        Assert.Equal(10, settings.GlowThreshold);
    }

    [Fact]
    public void RoundTripsSystemRuleAndThreshold()
    {
        var original = new ScoreColorSettings(ScoreColorSystem.Podium, GlowRule.TopPlaces, 3);

        var parsed = ScoreColorSettings.Parse(original.Serialize());

        Assert.Equal(original, parsed);
    }

    [Fact]
    public void UnknownFieldsAndValuesFallBackFieldByField()
    {
        var parsed = ScoreColorSettings.Parse("v1,system=Rainbow,glow=TopPercent,threshold=25,sparkle=yes");

        Assert.Equal(ScoreColorSystem.JudgementSpectrum, parsed.System);
        Assert.Equal(GlowRule.TopPercent, parsed.Glow);
        Assert.Equal(25, parsed.GlowThreshold);
    }

    [Fact]
    public void AValueWithoutTheVersionTokenIsTheDefault()
    {
        Assert.Equal(ScoreColorSettings.Default, ScoreColorSettings.Parse("system=None"));
    }

    [Fact]
    public void ThresholdsStayInsideOneToFiftyAndDefaultPerRule()
    {
        Assert.Equal(50, ScoreColorSettings.Clamp(GlowRule.TopPercent, 900));
        Assert.Equal(1, ScoreColorSettings.Clamp(GlowRule.TopPlaces, 0));
        Assert.Equal(1, ScoreColorSettings.Clamp(GlowRule.TopPlaces, null));
        Assert.Equal(10, ScoreColorSettings.Clamp(GlowRule.TopPercent, null));
    }

    [Theory]
    [InlineData(GlowRule.UnderPointsToNextGrade, 2500, GlowStrength.BrighterTheCloser)]
    [InlineData(GlowRule.LastPercentOfGrade, 100, GlowStrength.One)]
    public void RoundTripsAGradeRuleWithItsStrength(GlowRule rule, int threshold, GlowStrength strength)
    {
        var original = new ScoreColorSettings(ScoreColorSystem.SingleHue, rule, threshold, strength);

        Assert.Equal(original, ScoreColorSettings.Parse(original.Serialize()));
    }

    [Fact]
    public void GradeRuleLimitsAndDefaults()
    {
        Assert.Equal(5000, ScoreColorSettings.Clamp(GlowRule.UnderPointsToNextGrade, 9000));
        Assert.Equal(1000, ScoreColorSettings.Clamp(GlowRule.UnderPointsToNextGrade, null));
        Assert.Equal(100, ScoreColorSettings.Clamp(GlowRule.LastPercentOfGrade, 150));
        Assert.Equal(20, ScoreColorSettings.Clamp(GlowRule.LastPercentOfGrade, null));
        Assert.Equal(1, ScoreColorSettings.Clamp(GlowRule.LastPercentOfGrade, 0));
    }

    /// <summary>
    ///     A release from before the grade rules cannot parse the rule and falls back to Top 10%; a
    ///     2,500 written where it looks for a threshold would have read as the top 50% of peers.
    /// </summary>
    [Fact]
    public void AGradeRulesNumberIsWrittenWhereAnOlderReleaseDoesNotLookForIt()
    {
        var saved = new ScoreColorSettings(ScoreColorSystem.JudgementSpectrum, GlowRule.UnderPointsToNextGrade, 2500)
            .Serialize();

        Assert.Contains("near=2500", saved);
        Assert.DoesNotContain("threshold=", saved);
    }

    [Fact]
    public void ASaveWithoutAStrengthOrWithAnUnknownOneIsOneGlow()
    {
        Assert.Equal(GlowStrength.One,
            ScoreColorSettings.Parse("v1,system=Podium,glow=TopPlaces,threshold=3").Strength);
        var unknown = ScoreColorSettings.Parse("v1,glow=LastPercentOfGrade,near=30,strength=Sparkly");
        Assert.Equal(GlowStrength.One, unknown.Strength);
        Assert.Equal(30, unknown.GlowThreshold);
    }

    [Fact]
    public void OnlyTheActualGradeAndNoneIgnoreTheStanding()
    {
        Assert.False(new ScoreColorSettings(ScoreColorSystem.ActualGrade, GlowRule.Off, 1).UsesStanding);
        Assert.False(new ScoreColorSettings(ScoreColorSystem.None, GlowRule.Off, 1).UsesStanding);
        Assert.True(new ScoreColorSettings(ScoreColorSystem.Podium, GlowRule.Off, 1).UsesStanding);
    }
}
