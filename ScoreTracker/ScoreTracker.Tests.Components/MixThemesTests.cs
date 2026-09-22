using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services.Theming;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     A themed mix is one with its own palette, its own theme class and its own hue ramp, and
///     the account's theme override may name it. The Rise pair joined the three in phase 1
///     (docs/design/rise.md D15).
/// </summary>
public sealed class MixThemesTests
{
    [Theory]
    [InlineData(MixEnum.Rise, "theme-rise", "#FFC61A")]
    [InlineData(MixEnum.RiseArcade, "theme-rise-arcade", "#5AC5DE")]
    public void TheRiseMixesCarryTheirOwnPalettes(MixEnum mix, string cssClass, string primary)
    {
        Assert.Contains(mix, MixThemes.ThemedMixes);
        Assert.Equal(cssClass, MixThemes.CssClassFor(mix));
        Assert.Equal(primary, MixThemes.PaletteFor(mix).Primary);
        Assert.Equal(primary, MixThemes.HueHex(mix, 2));
        Assert.NotSame(MixThemes.PaletteFor(MixEnum.Phoenix), MixThemes.PaletteFor(mix));
        Assert.NotNull(MixThemes.ThemeFor(mix));
    }

    [Fact]
    public void TheAccountOverrideAcceptsARiseMix()
    {
        Assert.Equal(MixEnum.RiseArcade, MixThemes.ResolveThemeMix("RiseArcade", MixEnum.Phoenix));
        Assert.Equal(MixEnum.Phoenix2, MixThemes.ResolveThemeMix("Prime2", MixEnum.Phoenix2));
    }

    [Fact]
    public void AnUnthemedLegacyMixStillFallsBackToPhoenix()
    {
        Assert.Same(MixThemes.PaletteFor(MixEnum.Phoenix), MixThemes.PaletteFor(MixEnum.Prime2));
        Assert.Equal("theme-phoenix", MixThemes.CssClassFor(MixEnum.Prime2));
    }
}
