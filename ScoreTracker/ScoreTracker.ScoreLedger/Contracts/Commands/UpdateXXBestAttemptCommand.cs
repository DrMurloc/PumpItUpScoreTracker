using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record UpdateXXBestAttemptCommand(Guid chartId,
    XXLetterGrade? LetterGrade, bool IsBroken, XXScore? Score, MixEnum Mix = MixEnum.XX) : IRequest
{
}
