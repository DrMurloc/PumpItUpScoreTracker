using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed record Chart(Song Song, ChartType Type, DifficultyLevel Level)
{
    public string DifficultyString => $"{Type.GetShortHand()}{Level}";
    public int PlayerCount => Type == ChartType.CoOp ? Level : 1;
}