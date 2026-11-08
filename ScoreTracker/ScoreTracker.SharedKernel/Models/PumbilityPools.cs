using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.SharedKernel.Models;

/// <summary>
///     The two chart types a mix's PUMBILITY pools are made of: its Singles type and its Doubles
///     type, as the mix's folders define them. Rise's doubles are its half-doubles, so a pool of
///     doubles is never <see cref="ChartType.Double" /> by name (docs/design/rise.md D30).
/// </summary>
public static class PumbilityPools
{
    /// <summary>The Singles type, then the Doubles type, on this mix.</summary>
    public static IReadOnlyList<ChartType> TypesOn(MixEnum mix)
    {
        return new[] { SinglesOn(mix), DoublesOn(mix) };
    }

    public static ChartType SinglesOn(MixEnum mix)
    {
        return ChartTypeCategory.Single.TypeOn(mix);
    }

    public static ChartType DoublesOn(MixEnum mix)
    {
        return ChartTypeCategory.Double.TypeOn(mix);
    }
}
