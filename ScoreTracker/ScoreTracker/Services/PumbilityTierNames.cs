using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The PUMBILITY tier lists' names for the seven categories — Staple down to Poor, and the
///     phrase for a chart nobody holds (docs/design/pumbility-tier-list.md §7). One vocabulary for
///     the tier list's PUMBILITY lens and the peers page's prevalence tiers, which band the same
///     kind of count with the same rule and must read as one thing. The values are localization
///     keys; callers pass them through <c>L[…]</c>.
///     <para>
///         <see cref="PoolNameOf" /> is the second vocabulary, for the Breakdown page's top 50
///         (D57): those bands are what a chart is worth to <em>you</em>, not how many players keep
///         it, so they read as a magnitude — Highest down to Lowest — rather than as a share of a
///         crowd. Staple/Poor on your own fifty said the wrong thing entirely (owner, field test
///         round two). Average, Low and Very Low are the Score lens's own words, so the two ramps
///         still sound like one family.
///     </para>
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

    private static readonly IReadOnlyDictionary<TierListCategory, string> PoolNames = new Dictionary<TierListCategory, string>
    {
        [TierListCategory.Overrated] = "Highest",
        [TierListCategory.VeryEasy] = "Very High",
        [TierListCategory.Easy] = "High",
        [TierListCategory.Medium] = "Average",
        [TierListCategory.Hard] = "Low",
        [TierListCategory.VeryHard] = "Very Low",
        [TierListCategory.Underrated] = "Lowest",
        [TierListCategory.Unrecorded] = "Not Recorded"
    };

    public static string NameOf(TierListCategory category)
    {
        return Names[category];
    }

    /// <summary>What the band is called when it bands your own pool by value rather than a crowd.</summary>
    public static string PoolNameOf(TierListCategory category)
    {
        return PoolNames[category];
    }
}
