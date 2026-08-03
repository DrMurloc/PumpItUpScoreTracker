using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     A song in one mix, with the metadata the domain model has always carried and no consumer
///     has ever been able to read: artist, duration and BPM range.
///     <para>
///         Keyed by <see cref="Name" />, not an id. The catalog treats a song's name as its
///         identifier everywhere — <c>GetChartQuery</c> looks charts up by it — and the
///         <see cref="Song" /> domain model has no id field at all.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SongRecord(
    Name Name,
    SongType Type,
    Uri ImagePath,
    TimeSpan Duration,
    Name Artist,
    decimal? MinBpm,
    decimal? MaxBpm)
{
    public static SongRecord From(Song song)
    {
        return new SongRecord(song.Name, song.Type, song.ImagePath, song.Duration, song.Artist,
            song.Bpm?.Min, song.Bpm?.Max);
    }
}
