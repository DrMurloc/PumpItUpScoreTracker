using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed record Chart(Guid Id, Song Song, ChartType Type, DifficultyLevel Level, MixEnum Mix, Name? StepArtist,
    double? ScoringLevel,
    int? NoteCount)
{
    public string DifficultyString => $"{Type.GetShortHand()}{Level}";
    public int PlayerCount => Type == ChartType.CoOp ? Level : 1;
}