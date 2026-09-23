using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Services;

/// <summary>
///     Writes the grade and award tokens a Discord message carries (<c>#LETTERGRADE|…#</c>,
///     <c>#PLATE|…#</c>). A mix with its own score art names that set ahead of the value, so the bot
///     draws that mix's emoji; every other mix writes the bare token and draws the Phoenix set.
/// </summary>
public static class EmojiTokens
{
    public static string LetterGrade(MixEnum mix, PhoenixLetterGrade grade, bool isBroken)
    {
        return $"#LETTERGRADE|{ArtSet(mix)}{grade}|{isBroken}#";
    }

    /// <summary>An absent award writes the empty token, which the bot drops.</summary>
    public static string Plate(MixEnum mix, PhoenixPlate? plate)
    {
        return plate == null ? "#PLATE|#" : PlateNamed(mix, plate.Value.ToString());
    }

    /// <summary>For an award carried as its name, as a folder lamp's detail stores it.</summary>
    public static string PlateNamed(MixEnum mix, string plateName)
    {
        return $"#PLATE|{ArtSet(mix)}{plateName}#";
    }

    private static string ArtSet(MixEnum mix)
    {
        return MixProfiles.For(mix).Art.ScoreArtFolder is { } folder ? folder + "/" : string.Empty;
    }
}
