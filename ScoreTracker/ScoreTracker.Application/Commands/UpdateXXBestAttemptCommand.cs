using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands;

public sealed record UpdateXXBestAttemptCommand(Guid chartId,
    XXLetterGrade? LetterGrade, bool IsBroken, XXScore? Score) : IRequest
{
}