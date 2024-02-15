using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed record Song(Name Name, SongType Type, Uri ImagePath, TimeSpan Duration, Name? Artist, Bpm? Bpm)
{
}