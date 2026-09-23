namespace ScoreTracker.SharedKernel.Enums;

/// <summary>
///     What a chart <i>counts as</i>, as opposed to what it <i>is</i>. A folder is
///     (category, level) — the picker's tabs, the tier lists and their
///     <c>/TierLists/{category}/{level}</c> URLs, player-page folder completion, the By-Level
///     widget, the community boards and the randomizer's level weights all speak categories,
///     while the art, the scoring modifiers, chart identity, the upload vocabulary and the
///     <c>chartTypes</c> API parameter stay per type (docs/design/rise.md §3.1).
///     <para>
///         The members are named for the <see cref="ChartType" /> they head deliberately:
///         <c>Single</c> / <c>Double</c> / <c>CoOp</c> are the exact strings already sitting in
///         tier-list routes, in the <c>TierLists__ChartType</c> UiSetting and in API parameters,
///         so a category round-trips through every one of them with nothing migrated.
///     </para>
/// </summary>
public enum ChartTypeCategory
{
    Single,
    Double,
    CoOp
}

public static class ChartTypeCategories
{
    /// <summary>
    ///     A folder from a string a reader supplied — a route segment, a query parameter, a saved
    ///     setting. <see cref="Enum.TryParse{T}(string, bool, out T)" /> alone is not enough:
    ///     it happily accepts any number, so "/TierLists/7/20" would parse to an undefined
    ///     category and throw the first time anything asked it for its type or its shorthand.
    /// </summary>
    public static bool TryParse(string? text, out ChartTypeCategory category)
    {
        return Enum.TryParse(text, true, out category) && Enum.IsDefined(category);
    }

    /// <summary>The folder a chart of this type is browsed in.</summary>
    public static ChartTypeCategory Category(this ChartType type)
    {
        return type switch
        {
            ChartType.Single or ChartType.SinglePerformance => ChartTypeCategory.Single,
            // Double Performance has always counted as a double; the six-panel Half-Double
            // joined it when the category became the folder key (owner, 2026-09-22).
            ChartType.Double or ChartType.DoublePerformance or ChartType.HalfDouble =>
                ChartTypeCategory.Double,
            ChartType.CoOp => ChartTypeCategory.CoOp,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "This chart type has no category")
        };
    }

    /// <summary>Every chart type that counts in this folder, whether or not any mix has one.</summary>
    public static IReadOnlyList<ChartType> TypesIn(this ChartTypeCategory category)
    {
        return AllTypesIn[category];
    }

    /// <summary>Whether a chart of this type belongs in this folder.</summary>
    public static bool Includes(this ChartTypeCategory category, ChartType type)
    {
        return type.Category() == category;
    }

    /// <summary>
    ///     The folders this mix offers, in tab order, from the types on its profile. RISE answers
    ///     Singles and Doubles and no CoOp; its Doubles folder holds half-doubles.
    /// </summary>
    public static IReadOnlyList<ChartTypeCategory> CategoriesFor(MixEnum mix)
    {
        var types = MixProfiles.For(mix).ChartTypes;
        return Ordered.Where(category => types.Any(type => category.Includes(type))).ToArray();
    }

    /// <summary>
    ///     The mix's types that count in this folder, in <see cref="ChartType" /> order. A folder
    ///     holding exactly one of them is labeled by that type rather than by the category, which
    ///     is how RISE's second tab reads H. DOUBLE while Phoenix 2's reads Doubles.
    /// </summary>
    public static IReadOnlyList<ChartType> TypesOn(this ChartTypeCategory category, MixEnum mix)
    {
        return MixProfiles.For(mix).ChartTypes.Where(type => category.Includes(type)).OrderBy(t => t).ToArray();
    }

    /// <summary>
    ///     The chart type that heads this folder — Single, Double or CoOp. Queries, stored rows
    ///     and routes that predate the category speak this. A folder holding more than one of a
    ///     mix's types (the legacy line's doubles) needs <see cref="TypesOn" /> instead.
    /// </summary>
    public static ChartType HeadType(this ChartTypeCategory category)
    {
        return category switch
        {
            ChartTypeCategory.Single => ChartType.Single,
            ChartTypeCategory.Double => ChartType.Double,
            ChartTypeCategory.CoOp => ChartType.CoOp,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "This category has no type")
        };
    }

    /// <summary>
    ///     This folder's chart type <i>on this mix</i> — what a contract that still takes a single
    ///     <see cref="ChartType" /> has to be handed, because asking a mix for charts of a type it
    ///     does not have returns nothing. RISE's doubles folder answers HalfDouble, Phoenix 2's
    ///     answers Double. A folder holding several of a mix's types (the legacy line's doubles)
    ///     answers the first, which is the type that headed it before the category existed.
    /// </summary>
    public static ChartType TypeOn(this ChartTypeCategory category, MixEnum mix)
    {
        var types = category.TypesOn(mix);
        return types.Count == 0 ? category.HeadType() : types[0];
    }

    /// <summary>The shorthand a folder wears: <c>S</c>, <c>D</c> or <c>CoOp</c>.</summary>
    public static string GetShortHand(this ChartTypeCategory category)
    {
        return category.HeadType().GetShortHand();
    }

    private static readonly IReadOnlyList<ChartTypeCategory> Ordered = Enum.GetValues<ChartTypeCategory>();

    private static readonly IReadOnlyDictionary<ChartTypeCategory, IReadOnlyList<ChartType>> AllTypesIn =
        Enum.GetValues<ChartType>()
            .GroupBy(t => t.Category())
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ChartType>)g.OrderBy(t => t).ToArray());
}
