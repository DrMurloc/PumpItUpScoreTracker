using System.ComponentModel;
using System.Reflection;

namespace ScoreTracker.Domain.Enums;

public enum PhoenixPlate
{
    [Description("Rough Game")] RoughGame,
    [Description("Fair Game")] FairGame,
    [Description("Talented Game")] TalentedGame,
    [Description("Marvelous Game")] MarvelousGame,
    [Description("Superb Game")] SuperbGame,
    [Description("Extreme Game")] ExtremeGame,
    [Description("Ultimate Game")] UltimateGame,
    [Description("Perfect Game")] PerfectGame
}

public static class PhoenixPlateHelperMethods
{
    private static readonly IDictionary<string, PhoenixPlate> Parser =
        Enum.GetValues<PhoenixPlate>().ToDictionary(e => e.GetName());

    public static string GetName(this PhoenixPlate enumValue)
    {
        return typeof(PhoenixPlate).GetField(enumValue.ToString())?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description ?? enumValue.ToString();
    }

    public static PhoenixPlate? TryParse(string? value)
    {
        return value == null ? null : Parser.ContainsKey(value) ? Parser[value] : null;
    }
}