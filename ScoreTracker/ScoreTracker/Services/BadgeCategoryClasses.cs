using ScoreTracker.Catalog.Contracts;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The CSS class that carries a badge family's identity colour. One place, because three
///     surfaces tint the same vocabulary — the SRP's result chips, its skills facet cloud, and
///     the coverage bars on the chart page and its dialog — and a family that reads blue on
///     one and green on another would be worse than no colour at all.
///     The classes themselves live in site.css and read the --badgecat-* theme tokens, so no
///     markup carries a colour literal (docs/UX-GUIDELINES.md rule on tokens).
/// </summary>
public static class BadgeCategoryClasses
{
    public static string For(BadgeCategory? category)
    {
        return category == null
            ? string.Empty
            : $"badgecat-{category.Value.ToString().ToLowerInvariant()}";
    }
}
