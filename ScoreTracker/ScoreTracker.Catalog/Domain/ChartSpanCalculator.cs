using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     Pure span math for identity-grouped search results. Level Change compares only
///     appearances on the comparable modern numeric scale — Exceed onward, never slot-scale
///     rows (pre-Exceed levels live on per-era scales) and never co-ops (their historical
///     Level column is a player count). Ordering rides <see cref="MixEnumHelpers.DisplayOrder" />.
/// </summary>
internal static class ChartSpanCalculator
{
    internal sealed record Appearance(MixEnum Mix, int Level, bool OnSlotScale);

    private static readonly int ModernScaleStart = MixEnum.Exceed.DisplayOrder();

    public static MixEnum Latest(IEnumerable<MixEnum> mixes)
    {
        return mixes.OrderBy(m => m.DisplayOrder()).Last();
    }

    /// <summary>The mix whose appearance the result links to: the visitor's when present, else the latest.</summary>
    public static MixEnum LinkedMix(MixEnum preferred, IReadOnlyCollection<MixEnum> present)
    {
        return present.Contains(preferred) ? preferred : Latest(present);
    }

    public static int? LevelChange(IReadOnlyList<Appearance> appearances, bool isCoOp)
    {
        if (isCoOp) return null;

        var comparable = appearances
            .Where(a => !a.OnSlotScale && a.Mix.DisplayOrder() >= ModernScaleStart)
            .OrderBy(a => a.Mix.DisplayOrder())
            .ToArray();
        if (comparable.Length < 2) return null;

        return comparable[^1].Level - comparable[0].Level;
    }
}
