using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record UpdateXXBestAttemptCommand(Guid chartId,
    XXLetterGrade? LetterGrade, bool IsBroken, XXScore? Score) : IRequest
{
}
