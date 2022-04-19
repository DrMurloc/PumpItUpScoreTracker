using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed record Song(Name Name, Uri ImagePath)
{
}