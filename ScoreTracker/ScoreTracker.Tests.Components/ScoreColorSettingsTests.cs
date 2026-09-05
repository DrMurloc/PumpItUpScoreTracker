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

    [Fact]
    public void OnlyTheActualGradeAndNoneIgnoreTheStanding()
    {
        Assert.False(new ScoreColorSettings(ScoreColorSystem.ActualGrade, GlowRule.Off, 1).UsesStanding);
        Assert.False(new ScoreColorSettings(ScoreColorSystem.None, GlowRule.Off, 1).UsesStanding);
        Assert.True(new ScoreColorSettings(ScoreColorSystem.Podium, GlowRule.Off, 1).UsesStanding);
    }
}
