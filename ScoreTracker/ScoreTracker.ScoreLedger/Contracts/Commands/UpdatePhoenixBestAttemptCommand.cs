using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record UpdatePhoenixBestAttemptCommand(Guid ChartId,
    bool IsBroken, PhoenixScore? Score, PhoenixPlate? Plate, bool KeepBestStats = false,
    string Source = ScoreJournalEntry.ManualSource) : IRequest
{
}
