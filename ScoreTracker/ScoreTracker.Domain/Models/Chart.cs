using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed record Chart(Name SongName, ChartType Type, DifficultyLevel Level)
{
    public string DifficultyString => $"{Type.GetShortHand()}{Level}";
}