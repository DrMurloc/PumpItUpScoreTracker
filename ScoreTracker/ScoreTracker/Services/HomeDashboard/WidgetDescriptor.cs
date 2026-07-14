using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services.HomeDashboard;

/// <summary>Add-drawer grouping (docs/design/HomePageWidgets/README.md §2.2).</summary>
public enum WidgetCategory
{
    Play,
    Progress,
    Compete,
    Utility
}

/// <summary>
///     A widget footprint on the 4-column desktop grid (D5): columns × rows, tokenized
///     as "2x1" — the persisted and exported (D19) vocabulary. Mobile derives a single
///     column from auto-flow order (D3), so presets never need a mobile variant.
/// </summary>
public readonly record struct SizePreset(int Columns, int Rows)
{
    public static readonly SizePreset OneByOne = new(1, 1);
    public static readonly SizePreset TwoByOne = new(2, 1);
    public static readonly SizePreset ThreeByOne = new(3, 1);
    public static readonly SizePreset FourByOne = new(4, 1);
    public static readonly SizePreset OneByTwo = new(1, 2);
    public static readonly SizePreset OneByThree = new(1, 3);
    public static readonly SizePreset TwoByTwo = new(2, 2);
    public static readonly SizePreset ThreeByTwo = new(3, 2);
    public static readonly SizePreset FourByTwo = new(4, 2);
    public static readonly SizePreset ThreeByThree = new(3, 3);

    public string Token => $"{Columns}x{Rows}";

    public static SizePreset? TryParse(string? token)
    {
        if (token == null) return null;
        var parts = token.Split('x');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var columns) || columns is < 1 or > 4
            || !int.TryParse(parts[1], out var rows) || rows is < 1 or > 3)
            return null;
        return new SizePreset(columns, rows);
    }
}

/// <summary>
///     A pre-configured add-drawer entry (D10): the drawer shows one card per preset so
///     a multi-personality widget type stays discoverable. A preset is nothing but a
///     pre-filled ConfigJson — the created widget is an ordinary instance of the type.
/// </summary>
public sealed record WidgetDrawerPreset(string NameKey, string DescriptionKey, string ConfigJson);

/// <summary>
///     Everything the shell needs to know about a widget type (§2.2). Verticals own the
///     DATA (contract queries, precompute); Web owns descriptors + components because
///     Razor components live here (D15). TypeId is stable forever — it is the persisted
///     WidgetType and the public export/import vocabulary (D19). NameKey/DescriptionKey
///     are localization keys.
/// </summary>
public sealed record WidgetDescriptor(
    string TypeId,
    string NameKey,
    string DescriptionKey,
    WidgetCategory Category,
    string Icon,
    IReadOnlyList<SizePreset> SupportedSizes,
    SizePreset DefaultSize,
    IReadOnlyList<MixEnum> SupportedMixes,
    Type RenderComponent,
    Type? ConfigComponent,
    // The config RECORD type (not the panel) — the capability schema (D19) reflects it
    // into a JSON schema so AI-built dashboards know each widget's config vocabulary.
    Type? ConfigType = null,
    // When present, the add-drawer lists these instead of the single type entry.
    IReadOnlyList<WidgetDrawerPreset>? DrawerPresets = null,
    // Optional config-aware title: given an instance's ConfigJson, return the name KEY
    // to display (null → fall back to NameKey). Lets three rapid-fired presets of one
    // type wear distinct titles, live-localized, with no title stored per instance.
    Func<string, string?>? DynamicNameKey = null,
    // Optional header refresh action (owner, round 5): the host renders this icon in
    // the title bar and bumps the RefreshToken contract parameter — body real estate
    // stays with the content. TitleKey is the button's localized tooltip.
    string? RefreshIcon = null,
    string? RefreshTitleKey = null,
    // When true, the host auto-bumps RefreshToken after the viewer's score import lands, so
    // personal-score widgets reflect the new scores/rating without a manual refresh.
    bool RefreshOnScoreImport = false);
