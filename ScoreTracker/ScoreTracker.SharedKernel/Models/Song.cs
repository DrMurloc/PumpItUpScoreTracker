using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.SharedKernel.Models;

public sealed record Song(Name Name, SongType Type, Uri ImagePath, TimeSpan Duration, Name Artist, Bpm? Bpm)
{
}