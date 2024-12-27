using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed record Chart(Guid Id, MixEnum OriginalMix, Song Song, ChartType Type, DifficultyLevel Level, MixEnum Mix,
    Name? StepArtist,
    double? ScoringLevel,
    int? NoteCount,
    IReadOnlySet<Skill> Skills)
{
    public string DifficultyString => $"{Type.GetShortHand()}{Level}";
    public int PlayerCount => Type == ChartType.CoOp ? Level : 1;
}