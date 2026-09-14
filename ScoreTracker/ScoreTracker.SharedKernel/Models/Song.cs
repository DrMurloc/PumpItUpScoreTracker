using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.SharedKernel.Models;

/// <summary>
///     A piece of music. <see cref="Channel" /> is the folder it sits in on the cab of the mix
///     this record was built for — a song fact stored per mix, so the same song reads J-Music on
///     Phoenix and World Music on Phoenix 2 — and null where that mix has no row for it
///     (docs/design/song-channels.md §3). Trailing and optional so positional construction stays
///     as it was.
/// </summary>
public sealed record Song(Name Name, SongType Type, Uri ImagePath, TimeSpan Duration, Name Artist, Bpm? Bpm,
    Channel? Channel = null)
{
}
