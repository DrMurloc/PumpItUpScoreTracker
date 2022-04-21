using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands;

public sealed record RecordAttemptCommand(Name SongName, DifficultyLevel DifficultyLevel,
    ChartType ChartType, LetterGrade Grade, bool IsBroken) : IRequest<bool>
{
}