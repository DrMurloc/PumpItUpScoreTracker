using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands;

public sealed record UpdateBestAttemptCommand(Guid chartId,
    LetterGrade? LetterGrade, bool IsBroken, Score? Score) : IRequest
{
}