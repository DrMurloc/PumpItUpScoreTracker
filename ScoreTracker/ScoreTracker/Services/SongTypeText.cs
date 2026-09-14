using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services;

/// <summary>
///     How a song type reads on screen. The enum spells <c>ShortCut</c> and <c>FullSong</c>; a
///     player reads "Short Cut" and "Full Song". What comes back is the resx key as well as the
///     English text — keys are the English UI text verbatim — so a surface that names a song type
///     wraps this in <c>L[…]</c> and every locale gets its own word. Printing the enum value
///     instead is how the chart page and the details dialog served eight locales English.
/// </summary>
public static class SongTypeText
{
    public static string Name(SongType type)
    {
        return type switch
        {
            SongType.Arcade => "Arcade",
            SongType.ShortCut => "Short Cut",
            SongType.FullSong => "Full Song",
            SongType.Remix => "Remix",
            _ => type.ToString()
        };
    }
}
