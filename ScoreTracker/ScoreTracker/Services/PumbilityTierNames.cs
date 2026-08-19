using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The PUMBILITY tier lists' names for the seven categories — Staple down to Poor, and the
///     phrase for a chart nobody holds (docs/design/pumbility-tier-list.md §7). One vocabulary for
///     the tier list's PUMBILITY lens and the peers page's prevalence tiers, which band the same
///     kind of count with the same rule and must read as one thing. The values are localization
///     keys; callers pass them through <c>L[…]</c>.
/// </summary>
public static class PumbilityTierNames
{
    private static readonly IReadOnlyDictionary<TierListCategory, string> Names = new Dictionary<TierListCategory, string>
    {
        [TierListCategory.Overrated] = "Staple",
        [TierListCategory.VeryEasy] = "Strong",
        [TierListCategory.Easy] = "Solid",
        [TierListCategory.Medium] = "Average",
        [TierListCategory.Hard] = "Modest",
        [TierListCategory.VeryHard] = "Slim",
        [TierListCategory.Underrated] = "Poor",
        [TierListCategory.Unrecorded] = "Not in anyone's PUMBILITY"
    };

    /// <summary>The seven real tiers, best first — the order sections render in.</summary>
    public static readonly IReadOnlyList<TierListCategory> Tiers = new[]
    {
        TierListCategory.Overrated, TierListCategory.VeryEasy, TierListCategory.Easy, TierListCategory.Medium,
        TierListCategory.Hard, TierListCategory.VeryHard, TierListCategory.Underrated
    };

    public static string NameOf(TierListCategory category)
    {
        return Names[category];
    }
}
