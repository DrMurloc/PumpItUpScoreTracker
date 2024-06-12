using System.ComponentModel;
using System.Reflection;

namespace ScoreTracker.Domain.Enums;

public enum TournamentRole
{
    [Description("Head Tournament Organizer")]
    HeadTournamentOrganizer,
    [Description("Tournament Organizer")] TournamentOrganizer,
    [Description("Assistant")] Assistant
}

public static class TournamentRoleHelperMethods
{
    private static readonly IDictionary<TournamentRole, string> Parser =
        Enum.GetValues<TournamentRole>().ToDictionary(e => e, e => e.GetType().GetField(e.ToString())
            ?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description ?? "");

    public static string GetName(this TournamentRole enumValue)
    {
        return Parser[enumValue];
    }
}