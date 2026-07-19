using MudBlazor;
using ScoreTracker.SharedKernel.Enums;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

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
    // Chart series pair (Singles/Doubles) for render targets that can't read CSS vars
    // (ApexCharts config). CVD-validated per mix; era distinction rides line STYLE
    // (dashed), never a third hue — Combined died with the widget overhaul.
    string ChartSingles,
    string ChartDoubles,
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
        ChartSingles: "#FF2FA0",
        ChartDoubles: "#3B9EFF",
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
        ChartSingles: "#FF6B35",
        ChartDoubles: "#38B6FF",
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
        ChartSingles: "#E93CF2",
        ChartDoubles: "#29C9F7",
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

    // The plate/grade metal ladder, sampled from the game's Play Data plate art (owner,
    // 2026-07-13): copper RG/FG → silver TG/MG → gold SG/EG → ice-blue UG/PG. Letter grades
    // reuse these by tier (see GradeColors); sub-A grades render green in-game; a broken grade
    // is the grey we use for unpassed. Shared across mixes today. "None" marks unplayed charts.
    private const string PgHex = "#7FC7EF";
    private const string UgHex = "#62B4E8";
    private const string EgHex = "#F2C233";
    private const string SgHex = "#EDBB2E";
    private const string MgHex = "#D9DDE1";
    private const string TgHex = "#C4C9CE";
    private const string FgHex = "#DE863A";
    private const string RgHex = "#CE6A2E";
    private const string SubAGradeHex = "#34BE6A";
    private const string UnpassedGradeHex = "#5E5866";

    private static readonly IReadOnlyDictionary<PhoenixPlate, string> PlateColors =
        new Dictionary<PhoenixPlate, string>
        {
            [PhoenixPlate.PerfectGame] = PgHex,
            [PhoenixPlate.UltimateGame] = UgHex,
            [PhoenixPlate.ExtremeGame] = EgHex,
            [PhoenixPlate.SuperbGame] = SgHex,
            [PhoenixPlate.MarvelousGame] = MgHex,
            [PhoenixPlate.TalentedGame] = TgHex,
            [PhoenixPlate.FairGame] = FgHex,
            [PhoenixPlate.RoughGame] = RgHex
        };

    private const string PlateNoneColor = "#8E24AA";

    // Legacy slot colors: the classic song-wheel language of the pre-Exceed eras
    // (Crazy red, Freestyle green, Nightmare purple…). Deliberately NOT the difficulty
    // ramp — old-scale numbers don't translate to modern levels, and the distinct
    // vocabulary is the signal that a chip lives on a different scale
    // (docs/design/legacy-mixes.md). Another-variants reuse their base slot's hue.
    private static readonly IReadOnlyDictionary<string, string> SlotColors =
        new Dictionary<string, string>
        {
            ["easy"] = "#FDD835",
            ["normal"] = "#42A5F5",
            ["hard"] = "#FB8C00",
            ["crazy"] = "#E53935",
            ["freestyle"] = "#43A047",
            ["nightmare"] = "#8E24AA",
            ["practice"] = "#9E9E9E",
            ["another"] = "#00ACC1",
            // Non-slot legacy chips (Half-Double, levelled co-ops, unrated) read neutral.
            ["neutral"] = "#90A4AE"
        };

    // Third-party sign-in brand marks (Discord blurple, Google blue, PIUGAME red).
    // Mix-invariant like the skill categories — brand identity never re-hues — and
    // emitted as tokens so the front door and sign-in card ship with zero literals
    // (docs/design/front-door.md, C3).
    private static readonly IReadOnlyDictionary<string, string> BrandColors =
        new Dictionary<string, string>
        {
            ["discord"] = "#5865F2",
            ["google"] = "#4285F4",
            ["piugame"] = "#C4302B"
        };

    /// <summary>
    ///     Raw hex for a difficulty category — for render targets that can't read CSS
    ///     custom properties (the SkiaSharp share card). On-screen consumers keep using
    ///     ThemeScales/var(--diff-*).
    /// </summary>
    public static string DifficultyHex(TierListCategory category)
    {
        return DifficultyColors[category];
    }

    /// <summary>
    ///     Raw hex for a rarity band in a mix — for render targets that can't read CSS
    ///     custom properties (ApexCharts config, e.g. the By-Level Breakdown grade/plate
    ///     bars). Rarity hues are per-mix (unlike difficulty), so this takes the mix.
    ///     On-screen consumers keep using ThemeScales/var(--rarity-*).
    /// </summary>
    public static string RarityHex(MixEnum mix, RarityBand band)
    {
        var rarity = PaletteFor(mix).Rarity;
        return band switch
        {
            RarityBand.Common => rarity.Common,
            RarityBand.Silver => rarity.Silver,
            RarityBand.Emerald => rarity.Emerald,
            RarityBand.Gold => rarity.Gold,
            RarityBand.Sapphire => rarity.Sapphire,
            _ => rarity.Prism
        };
    }

    // Letter grades wear the PLATE colors by tier (owner, 2026-07-13): SSS+/SSS = PG/UG
    // ice-blue, SS/S = EG/SG gold, AAA+/AAA = MG/TG silver, AA/A = FG/RG copper, and
    // everything below A is the in-game sub-A green. Grades render as art everywhere else;
    // the By-Level Breakdown bars are the first text-rendered consumer (UX-GUIDELINES §4).
    // Keyed by display name so Phoenix (16) and the XX ladder (F..SSS) both resolve.
    private static readonly IReadOnlyDictionary<string, string> GradeColors =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SSS+"] = PgHex, ["SSS"] = UgHex,
            ["SS+"] = EgHex, ["SS"] = EgHex, ["S+"] = SgHex, ["S"] = SgHex,
            ["AAA+"] = MgHex, ["AAA"] = TgHex,
            ["AA+"] = FgHex, ["AA"] = FgHex, ["A+"] = RgHex, ["A"] = RgHex,
            ["B"] = SubAGradeHex, ["C"] = SubAGradeHex, ["D"] = SubAGradeHex, ["F"] = SubAGradeHex
        };

    // The difficulty-ball vocabulary (Phoenix 2 art): red Single, green Double, gold Co-Op.
    private const string SinglesTypeHex = "#C83A32";
    private const string DoublesTypeHex = "#33A653";
    private const string CoOpTypeHex = "#D9A82E";

    /// <summary>Raw hex for a letter grade (display name), for ApexCharts grade bars. Unknown → the unpassed grey.</summary>
    public static string GradeHex(string gradeName) =>
        GradeColors.TryGetValue(gradeName, out var hex) ? hex : UnpassedGradeHex;

    /// <summary>
    ///     Playstyle archetype color — the archetypes are letter-grade bands over a player's
    ///     top-Pumbility average, so each wears its band's grade color: sub-A green for Pass
    ///     Pusher, AAA silver for Pass Refiner, S gold for Balanced, then the SSS/SSS+ ice-blues.
    /// </summary>
    public static string PlayerTypeHex(RecapPlayerType type) => type switch
    {
        RecapPlayerType.PassPusher => SubAGradeHex,
        RecapPlayerType.PassRefiner => TgHex,
        RecapPlayerType.BalancedPlayer => SgHex,
        RecapPlayerType.Competitive => UgHex,
        _ => PgHex
    };

    /// <summary>Raw hex for a plate (shorthand, e.g. "PG"), for ApexCharts plate bars.</summary>
    public static string PlateHex(string plateShorthand) =>
        PlateColors[PhoenixPlateHelperMethods.ParseShorthand(plateShorthand)];

    /// <summary>Chart-type color — red Single / green Double / gold Co-Op, the game's ball vocabulary.</summary>
    public static string ChartTypeHex(ChartType type) => type switch
    {
        ChartType.Single or ChartType.SinglePerformance => SinglesTypeHex,
        ChartType.Double or ChartType.DoublePerformance or ChartType.HalfDouble => DoublesTypeHex,
        ChartType.CoOp => CoOpTypeHex,
        _ => SinglesTypeHex
    };

    /// <summary>Muted grey for unpassed / not-cleared / below-threshold segments (the broken-grade grey).</summary>
    public static string UnpassedHex => UnpassedGradeHex;

    // Qualitative series palette for chart lines that carry no semantic-ramp meaning
    // (By-Level Breakdown distribution stats and completion thresholds). ApexCharts needs
    // literals; these are CVD-spaced and distinct on the dark canvas. Mix-invariant.
    private static readonly string[] SeriesPalette =
    {
        "#38BDF8", "#E879F9", "#F5A524", "#34D399", "#F43F5E", "#A78BFA", "#22D3EE", "#FB923C"
    };

    /// <summary>Nth qualitative chart-series hex (wraps). For ApexCharts, which can't read CSS vars.</summary>
    public static string SeriesHex(int index) =>
        SeriesPalette[((index % SeriesPalette.Length) + SeriesPalette.Length) % SeriesPalette.Length];

    public static string CssVariablesFor(MixEnum mix)
    {
        var p = PaletteFor(mix);
        var difficulty = string.Join("\n", DifficultyColors.Select(kv =>
            $"    --diff-{ThemeScales.DifficultySlug(kv.Key)}: {kv.Value};"));
        var plates = string.Join("\n", PlateColors.Select(kv =>
            $"    --plate-{kv.Key.GetShorthand().ToLowerInvariant()}: {kv.Value};"))
            + $"\n    --plate-none: {PlateNoneColor};"
            + "\n" + string.Join("\n", SlotColors.Select(kv => $"    --slot-{kv.Key}: {kv.Value};"));
        // The five skill-category identity colors (Speed/Stamina/Twist/Bracket/Tech),
        // promoted from the SharedKernel constants so markup can tint skill chips
        // without color literals. Mix-invariant — category identity never re-hues.
        var skillCategories = string.Join("\n", Enum.GetValues<SkillCategory>().Select(c =>
            $"    --skillcat-{c.ToString().ToLowerInvariant()}: {c.GetColor()};"));
        var brands = string.Join("\n", BrandColors.Select(kv =>
            $"    --brand-{kv.Key}: {kv.Value};"));
        // The difficulty-ball type vocabulary (red Single / green Double / gold Co-Op) as
        // tokens, so markup can stack singles-vs-doubles segments without literals.
        var chartTypes = $"    --type-singles: {SinglesTypeHex};\n" +
                         $"    --type-doubles: {DoublesTypeHex};\n" +
                         $"    --type-coop: {CoOpTypeHex};";
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
    --chart-singles: {p.ChartSingles};
    --chart-doubles: {p.ChartDoubles};
    --rarity-common: {p.Rarity.Common};
    --rarity-silver: {p.Rarity.Silver};
    --rarity-emerald: {p.Rarity.Emerald};
    --rarity-gold: {p.Rarity.Gold};
    --rarity-sapphire: {p.Rarity.Sapphire};
    --rarity-prism: {p.Rarity.Prism};
{difficulty}
{plates}
{skillCategories}
{brands}
{chartTypes}
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
