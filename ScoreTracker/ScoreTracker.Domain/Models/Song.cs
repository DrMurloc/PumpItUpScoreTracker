using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed record Song(Name Name, Uri ImagePath)
{
    public SongType Type
    {
        get
        {
            var nameString = (string)Name;
            if (nameString.EndsWith("Full Song")) return SongType.FullSong;
            if (nameString.EndsWith("Remix")) return SongType.Remix;
            return nameString.EndsWith("Short Cut") ? SongType.ShortCut : SongType.Arcade;
        }
    }
}