using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Infrastructure;

/// <summary>
///     Plate round-tripping for this vertical's tables, which store the enum NAME
///     ("PerfectGame") — not the <c>GetName()</c> spelling ("Perfect Game") the score ledger's
///     tables use, so <c>PhoenixPlateHelperMethods.TryParse</c> would read every row as null
///     here. Null is a broken entry, which is awarded no plate.
/// </summary>
internal static class PlateColumn
{
    public static PhoenixPlate? Read(string? stored)
    {
        return Enum.TryParse<PhoenixPlate>(stored, out var plate) ? plate : null;
    }

    public static string? Write(PhoenixPlate? plate)
    {
        return plate?.ToString();
    }
}
