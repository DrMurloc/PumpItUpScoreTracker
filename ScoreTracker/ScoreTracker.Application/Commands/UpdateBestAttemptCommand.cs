using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands;

public sealed record UpdateBestAttemptCommand(Name SongName, ChartType ChartType, DifficultyLevel Level,
    LetterGrade? LetterGrade, bool IsBroken) : IRequest
{
}