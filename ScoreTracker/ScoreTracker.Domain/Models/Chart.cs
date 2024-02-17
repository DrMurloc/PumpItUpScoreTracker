using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed record Chart(Guid Id, Song Song, ChartType Type, DifficultyLevel Level, Name? StepArtist, int? NoteCount)
{
    public string DifficultyString => $"{Type.GetShortHand()}{Level}";
    public int PlayerCount => Type == ChartType.CoOp ? Level : 1;

    public string DifficultyBubblePath =>
        $"https://piuimages.arroweclip.se/difficulty/{DifficultyString.ToLower()}.png";
}