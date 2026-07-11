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
    string Glow,
    RarityRamp Rarity)
{
    // Semantic alert colors are intentionally shared across mixes: red/green/amber alerts
    // must read identically no matter which theme is active.
    public const string Error = "#C72020";
    public const string Success = "#6EDE7F";
    public const string Warning = "#FFC433";

    // Matches MudBlazor's default Info blue — the To-Do border reads var(--mud-palette-info),
    // and render targets that can't read CSS vars (the share card) read this.
    public const string Info = "#2196F3";
}

/// <summary>
/// The rarity scale: how good is this relative to the population (score percentiles,
/// leaderboard positions). Starts at neutral grey — a low percentile is common, not a
/// failure — and climbs the ladder PIU's own plate metals taught players: silver, gold,
/// then blue as the elite anchor (SSS grades, UG/PG plates, Perfect judgments all agree).
/// Names describe the color on purpose (show-don't-tell survives localization).
/// Hues are tuned per mix; band meaning and order never change.
/// </summary>
public sealed record RarityRamp(
    string Common,
    string Silver,
    string Emerald,
    string Gold,
    string Sapphire,
    string Prism);

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
        Glow: "rgba(53, 200, 255, .40)",
        Rarity: new RarityRamp(
            Common: "#958CA6",
            Silver: "#D5CDE3",
            Emerald: "#2ECC40",
            Gold: "#FFD91C",
            Sapphire: "#3B9EFF",
            Prism: "#F0E9FF"));

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
        Glow: "rgba(63, 169, 245, .40)",
        Rarity: new RarityRamp(
            Common: "#8B98A9",
            Silver: "#CBD5E1",
            Emerald: "#3FD35A",
            Gold: "#FFD24A",
            Sapphire: "#38B6FF",
            Prism: "#9BE9FF"));

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
        Glow: "rgba(79, 227, 63, .40)",
        Rarity: new RarityRamp(
            Common: "#8FA396",
            Silver: "#CBDCD0",
            Emerald: "#46D838",
            Gold: "#FFC72E",
            Sapphire: "#29C9F7",
            Prism: "#E9FFD9"));

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
    // The difficulty scale: how hard is this chart relative to its level (tier lists).
    // Green→red heat — red means danger here exactly as it does in-game, which is why the
    // rarity ramp never uses it. Shared across mixes today; emitted as vars so a per-mix
    // tune later costs no consumer churn. Values are the pre-façade canonical hues
    // (ChartSkills/Pumbility) — the two neon-dialect clones normalize onto these.
    private static readonly IReadOnlyDictionary<TierListCategory, string> DifficultyColors =
        new Dictionary<TierListCategory, string>
        {
            [TierListCategory.Overrated] = "#00ACC1",
            [TierListCategory.VeryEasy] = "#43A047",
            [TierListCategory.Easy] = "#7CB342",
            [TierListCategory.Medium] = "#FDD835",
            [TierListCategory.Hard] = "#FB8C00",
            [TierListCategory.VeryHard] = "#E53935",
            [TierListCategory.Underrated] = "#8E24AA",
            [TierListCategory.Unrecorded] = "#757575"
        };

    // Plate chip/border colors, following the official Play Data page's metal ladder:
    // bronze (RG/FG) → silver (TG/MG) → gold (SG/EG) → ice-blue (UG/PG). Shared across
    // mixes today; when the owner's Phoenix 2 plate art arrives (P2's colors are close
    // but not identical), these lift into MixPalette per mix — consumers already read
    // the vars, so that change costs nothing downstream. "None" marks unplayed charts.
    private static readonly IReadOnlyDictionary<PhoenixPlate, string> PlateColors =
        new Dictionary<PhoenixPlate, string>
        {
            [PhoenixPlate.PerfectGame] = "#6FD1F6",
            [PhoenixPlate.UltimateGame] = "#4FB3E8",
            [PhoenixPlate.ExtremeGame] = "#FFD24A",
            [PhoenixPlate.SuperbGame] = "#F5C02E",
            [PhoenixPlate.MarvelousGame] = "#C9CED4",
            [PhoenixPlate.TalentedGame] = "#B4BCC4",
            [PhoenixPlate.FairGame] = "#D97742",
            [PhoenixPlate.RoughGame] = "#C05C2E"
        };

    private const string PlateNoneColor = "#8E24AA";

    /// <summary>
    ///     Raw hex for a difficulty category — for render targets that can't read CSS
    ///     custom properties (the SkiaSharp share card). On-screen consumers keep using
    ///     ThemeScales/var(--diff-*).
    /// </summary>
    public static string DifficultyHex(TierListCategory category)
    {
        return DifficultyColors[category];
    }

    public static string CssVariablesFor(MixEnum mix)
    {
        var p = PaletteFor(mix);
        var difficulty = string.Join("\n", DifficultyColors.Select(kv =>
            $"    --diff-{ThemeScales.DifficultySlug(kv.Key)}: {kv.Value};"));
        var plates = string.Join("\n", PlateColors.Select(kv =>
            $"    --plate-{kv.Key.GetShorthand().ToLowerInvariant()}: {kv.Value};"))
            + $"\n    --plate-none: {PlateNoneColor};";
        // The five skill-category identity colors (Speed/Stamina/Twist/Bracket/Tech),
        // promoted from the SharedKernel constants so markup can tint skill chips
        // without color literals. Mix-invariant — category identity never re-hues.
        var skillCategories = string.Join("\n", Enum.GetValues<SkillCategory>().Select(c =>
            $"    --skillcat-{c.ToString().ToLowerInvariant()}: {c.GetColor()};"));
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
    --rarity-common: {p.Rarity.Common};
    --rarity-silver: {p.Rarity.Silver};
    --rarity-emerald: {p.Rarity.Emerald};
    --rarity-gold: {p.Rarity.Gold};
    --rarity-sapphire: {p.Rarity.Sapphire};
    --rarity-prism: {p.Rarity.Prism};
{difficulty}
{plates}
{skillCategories}
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
