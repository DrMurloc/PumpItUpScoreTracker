using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.SharedKernel.Models;

public sealed record Chart(Guid Id, MixEnum OriginalMix, Song Song, ChartType Type, DifficultyLevel Level, MixEnum Mix,
    Name? StepArtist,
    int? NoteCount,
    IReadOnlySet<Skill> Skills)
{
    public string DifficultyString => $"{Type.GetShortHand()}{Level}";
    public int PlayerCount => Type == ChartType.CoOp ? Level : 1;
}