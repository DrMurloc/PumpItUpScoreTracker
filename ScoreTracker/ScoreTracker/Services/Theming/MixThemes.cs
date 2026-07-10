using MudBlazor;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services.Theming;

/// <summary>
/// One brand palette per playable mix. A palette is the single source of truth for that
/// mix's look: it builds the MudBlazor <see cref="MudTheme"/> AND emits the --mix-* CSS
/// custom properties, so Mud components and hand-styled markup can never drift apart.
/// Values were calibrated against in-game and official-site captures (2026-07-10):
/// XX = violet ground with dueling magenta/cyan neon; Phoenix = cosmic blue with fire and
/// gold accents; Phoenix 2 = acid green on teal space with electric magenta.
/// The site is dark-only by design — PaletteLight and PaletteDark are deliberately the
/// same palette, matching how the pre-theme site already behaved.
/// </summary>
public sealed record MixPalette(
    string Background,
    string Surface,
    string SurfaceMuted,
    string Nav,
    string Primary,
    string PrimaryContrast,
    string Secondary,
    string Accent,
    string Ink,
    string InkMuted,
    string Glow)
{
    // Semantic alert colors are intentionally shared across mixes: red/green/amber alerts
    // must read identically no matter which theme is active.
    public const string Error = "#C72020";
    public const string Success = "#6EDE7F";
    public const string Warning = "#FFC433";
}

public static class MixThemes
{
    /// <summary>UiSettings key for the account-level theme override.</summary>
    public const string OverrideSettingKey = "Universal__ThemeOverride";

    /// <summary>Override value meaning "no override — theme follows the selected mix".</summary>
    public const string MatchMixValue = "MatchMix";

    /// <summary>Mixes that have a calibrated theme (also the /Account override choices).</summary>
    public static readonly MixEnum[] ThemedMixes = { MixEnum.XX, MixEnum.Phoenix, MixEnum.Phoenix2 };

    // Latin glyphs and all numerals render in the display face; Korean/Japanese glyphs fall
    // through per-glyph to bold-capable system CJK fonts, so mixed-script headings keep weight.
    private static readonly string[] DisplayFontStack =
    {
        "Barlow Condensed", "Malgun Gothic", "Yu Gothic UI", "Apple SD Gothic Neo", "Hiragino Sans",
        "sans-serif"
    };

    private static readonly MixPalette XX = new(
        Background: "#0B0714",
        Surface: "#171029",
        SurfaceMuted: "#221A38",
        Nav: "#140C24",
        Primary: "#FF2FA0",
        PrimaryContrast: "#FFFFFF",
        Secondary: "#35C8FF",
        Accent: "#FFD91C",
        Ink: "#F2E9F5",
        InkMuted: "#A99BB8",
        // Cyan glow under magenta primaries = the XX dueling-neon signature.
        Glow: "rgba(53, 200, 255, .40)");

    private static readonly MixPalette Phoenix = new(
        Background: "#070B15",
        Surface: "#161E2C",
        SurfaceMuted: "#1E2A3D",
        Nav: "#0D1626",
        Primary: "#3FA9F5",
        PrimaryContrast: "#06101C",
        Secondary: "#FF6B35",
        Accent: "#FFD24A",
        Ink: "#E9EFF7",
        InkMuted: "#93A3B8",
        Glow: "rgba(63, 169, 245, .40)");

    private static readonly MixPalette Phoenix2 = new(
        Background: "#060D08",
        Surface: "#14201A",
        SurfaceMuted: "#1C2B22",
        Nav: "#0B1810",
        Primary: "#4FE33F",
        // Acid green is too bright to carry white button text.
        PrimaryContrast: "#06130A",
        Secondary: "#2BC1D8",
        Accent: "#E93CF2",
        Ink: "#EAF5EC",
        InkMuted: "#9AB3A3",
        Glow: "rgba(79, 227, 63, .40)");

    private static readonly IReadOnlyDictionary<MixEnum, MudTheme> Themes =
        new[] { MixEnum.XX, MixEnum.Phoenix, MixEnum.Phoenix2 }
            .ToDictionary(m => m, m => Build(PaletteFor(m)));

    public static MixPalette PaletteFor(MixEnum mix) => mix switch
    {
        MixEnum.XX => XX,
        MixEnum.Phoenix2 => Phoenix2,
        _ => Phoenix
    };

    public static MudTheme ThemeFor(MixEnum mix) =>
        Themes.TryGetValue(mix, out var theme) ? theme : Themes[MixEnum.Phoenix];

    /// <summary>
    /// Account override wins; anything unparseable (including <see cref="MatchMixValue"/>
    /// and null) falls through to the currently selected mix.
    /// </summary>
    public static MixEnum ResolveThemeMix(string? overrideSetting, MixEnum currentMix) =>
        Enum.TryParse<MixEnum>(overrideSetting, out var mix) && ThemedMixes.Contains(mix)
            ? mix
            : currentMix;

    /// <summary>Theme marker class for the layout root — atmosphere and page CSS key off it.</summary>
    public static string CssClassFor(MixEnum mix) => mix switch
    {
        MixEnum.XX => "theme-xx",
        MixEnum.Phoenix2 => "theme-phoenix2",
        _ => "theme-phoenix"
    };

    /// <summary>
    /// The --mix-* custom properties for the active palette. Rendered into a style block by
    /// the layout; pages style against the tokens, never against literal colors.
    /// </summary>
    public static string CssVariablesFor(MixEnum mix)
    {
        var p = PaletteFor(mix);
        return $@":root {{
    --mix-bg: {p.Background};
    --mix-surface: {p.Surface};
    --mix-surface-muted: {p.SurfaceMuted};
    --mix-nav: {p.Nav};
    --mix-primary: {p.Primary};
    --mix-primary-contrast: {p.PrimaryContrast};
    --mix-secondary: {p.Secondary};
    --mix-accent: {p.Accent};
    --mix-ink: {p.Ink};
    --mix-ink-muted: {p.InkMuted};
    --mix-glow: {p.Glow};
}}";
    }

    private static MudTheme Build(MixPalette p)
    {
        // Dark-only: both palettes get the same values so the theme holds regardless of
        // which palette MudBlazor resolves.
        return new MudTheme
        {
            PaletteLight = new PaletteLight
            {
                Primary = p.Primary,
                PrimaryContrastText = p.PrimaryContrast,
                Secondary = p.Secondary,
                SecondaryContrastText = p.PrimaryContrast,
                Tertiary = p.Accent,
                Error = MixPalette.Error,
                Success = MixPalette.Success,
                Warning = MixPalette.Warning,
                TextPrimary = p.Ink,
                TextSecondary = p.InkMuted,
                ActionDefault = p.Ink,
                ActionDisabled = "#d3d3d3",
                TextDisabled = "#d3d3d3",
                Background = p.Background,
                BackgroundGray = p.SurfaceMuted,
                Surface = p.Surface,
                AppbarBackground = p.Nav,
                AppbarText = p.Ink,
                DrawerBackground = p.Nav,
                DrawerIcon = p.Ink,
                DrawerText = p.Ink
            },
            PaletteDark = new PaletteDark
            {
                Primary = p.Primary,
                PrimaryContrastText = p.PrimaryContrast,
                Secondary = p.Secondary,
                SecondaryContrastText = p.PrimaryContrast,
                Tertiary = p.Accent,
                Error = MixPalette.Error,
                Success = MixPalette.Success,
                Warning = MixPalette.Warning,
                TextPrimary = p.Ink,
                TextSecondary = p.InkMuted,
                ActionDefault = p.Ink,
                ActionDisabled = "#d3d3d3",
                TextDisabled = "#d3d3d3",
                Background = p.Background,
                BackgroundGray = p.SurfaceMuted,
                Surface = p.Surface,
                AppbarBackground = p.Nav,
                AppbarText = p.Ink,
                DrawerBackground = p.Nav,
                DrawerIcon = p.Ink,
                DrawerText = p.Ink
            },
            Typography = new Typography
            {
                H1 = new H1Typography { FontFamily = DisplayFontStack },
                H2 = new H2Typography { FontFamily = DisplayFontStack },
                H3 = new H3Typography { FontFamily = DisplayFontStack },
                H4 = new H4Typography { FontFamily = DisplayFontStack },
                H5 = new H5Typography { FontFamily = DisplayFontStack },
                H6 = new H6Typography { FontFamily = DisplayFontStack }
            }
        };
    }
}
