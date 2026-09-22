namespace ScoreTracker.SharedKernel.Enums;

/// <summary>
///     The awards a mix hands out beside the grade — which <see cref="PhoenixPlate" /> values it
///     uses, and what each is called and abbreviated there. The stored value is always the
///     Phoenix plate; only the vocabulary changes per set, so a record never has to know which
///     mix's words it was written under.
/// </summary>
public static class AwardSets
{
    private static readonly IReadOnlyList<PhoenixPlate> AllPlates = Enum.GetValues<PhoenixPlate>();

    /// <summary>The plates a set awards, in the enum's ascending order; empty for a mix that awards nothing.</summary>
    public static IReadOnlyList<PhoenixPlate> PlatesOf(AwardSet set)
    {
        return set switch
        {
            AwardSet.PhoenixPlates => AllPlates,
            _ => Array.Empty<PhoenixPlate>()
        };
    }

    /// <summary>The plates this mix awards — what a record form offers and a spreadsheet may carry.</summary>
    public static IReadOnlyList<PhoenixPlate> AwardsOf(this MixEnum mix)
    {
        return PlatesOf(MixProfiles.For(mix).Awards);
    }

    /// <summary>The shorthand a plate wears on this mix (<c>PG</c>, <c>UG</c> …).</summary>
    public static string GetShorthand(this PhoenixPlate plate, MixEnum mix)
    {
        return plate.GetShorthand();
    }

    /// <summary>The name a plate wears on this mix.</summary>
    public static string GetName(this PhoenixPlate plate, MixEnum mix)
    {
        return plate.GetName();
    }

    /// <summary>Reads a shorthand as this mix writes it, case-insensitively; null for anything the mix does not award.</summary>
    public static PhoenixPlate? TryParseShorthand(string? value, MixEnum mix)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var plate = PhoenixPlateHelperMethods.TryParseShorthand(value.Trim());
        return plate != null && mix.AwardsOf().Contains(plate.Value) ? plate : null;
    }

    public static PhoenixPlate ParseShorthand(string value, MixEnum mix)
    {
        return TryParseShorthand(value, mix)
               ?? throw new ArgumentOutOfRangeException(nameof(value), value, "Not an award this mix hands out");
    }
}
