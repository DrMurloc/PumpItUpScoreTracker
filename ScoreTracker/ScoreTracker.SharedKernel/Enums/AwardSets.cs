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

    // Rise's marks, stored as the plates they coincide with (docs/design/rise.md D5): a run
    // with every note Perfect is a Perfect Game, one with nothing below Great a Full Combo,
    // one with no Miss a No Miss. Ascending, like the plate enum.
    private static readonly IReadOnlyList<PhoenixPlate> RiseMarks = new[]
        { PhoenixPlate.SuperbGame, PhoenixPlate.UltimateGame, PhoenixPlate.PerfectGame };

    private static readonly IReadOnlyDictionary<PhoenixPlate, (string Shorthand, string Name)> RiseMarkWords =
        new Dictionary<PhoenixPlate, (string, string)>
        {
            [PhoenixPlate.PerfectGame] = ("PG", "Perfect Game"),
            [PhoenixPlate.UltimateGame] = ("FC", "Full Combo"),
            [PhoenixPlate.SuperbGame] = ("NM", "No Miss")
        };

    private static readonly IReadOnlyDictionary<string, PhoenixPlate> RiseMarkParser =
        RiseMarkWords.ToDictionary(kv => kv.Value.Shorthand, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>The plates a set awards, in the enum's ascending order; empty for a mix that awards nothing.</summary>
    public static IReadOnlyList<PhoenixPlate> PlatesOf(AwardSet set)
    {
        return set switch
        {
            AwardSet.PhoenixPlates => AllPlates,
            AwardSet.RiseMarks => RiseMarks,
            _ => Array.Empty<PhoenixPlate>()
        };
    }

    /// <summary>The plates this mix awards — what a record form offers and a spreadsheet may carry.</summary>
    public static IReadOnlyList<PhoenixPlate> AwardsOf(this MixEnum mix)
    {
        return PlatesOf(MixProfiles.For(mix).Awards);
    }

    /// <summary>The shorthand a plate wears on this mix (<c>PG</c>, <c>UG</c> … or Rise's <c>FC</c> and <c>NM</c>).</summary>
    public static string GetShorthand(this PhoenixPlate plate, MixEnum mix)
    {
        return MixProfiles.For(mix).Awards == AwardSet.RiseMarks && RiseMarkWords.TryGetValue(plate, out var mark)
            ? mark.Shorthand
            : plate.GetShorthand();
    }

    /// <summary>The name a plate wears on this mix.</summary>
    public static string GetName(this PhoenixPlate plate, MixEnum mix)
    {
        return MixProfiles.For(mix).Awards == AwardSet.RiseMarks && RiseMarkWords.TryGetValue(plate, out var mark)
            ? mark.Name
            : plate.GetName();
    }

    /// <summary>
    ///     Reads a shorthand as this mix writes it, case-insensitively; null for anything the mix
    ///     does not award. A Rise mark also reads as the plate code it is stored under, so a
    ///     spreadsheet written either way imports.
    /// </summary>
    public static PhoenixPlate? TryParseShorthand(string? value, MixEnum mix)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        var awards = mix.AwardsOf();
        if (MixProfiles.For(mix).Awards == AwardSet.RiseMarks && RiseMarkParser.TryGetValue(trimmed, out var mark))
            return mark;
        var plate = PhoenixPlateHelperMethods.TryParseShorthand(trimmed);
        return plate != null && awards.Contains(plate.Value) ? plate : null;
    }

    public static PhoenixPlate ParseShorthand(string value, MixEnum mix)
    {
        return TryParseShorthand(value, mix)
               ?? throw new ArgumentOutOfRangeException(nameof(value), value, "Not an award this mix hands out");
    }
}
