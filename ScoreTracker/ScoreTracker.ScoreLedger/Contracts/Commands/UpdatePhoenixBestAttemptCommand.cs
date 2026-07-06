using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record UpdatePhoenixBestAttemptCommand(Guid ChartId,
    bool IsBroken, PhoenixScore? Score, PhoenixPlate? Plate, bool KeepBestStats = false,
    string Source = ScoreJournalEntry.ManualSource, MixEnum Mix = MixEnum.Phoenix,
    Guid? SessionId = null) : IRequest
{
}
