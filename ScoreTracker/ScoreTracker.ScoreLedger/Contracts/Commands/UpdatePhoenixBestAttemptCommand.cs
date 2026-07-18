using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record UpdatePhoenixBestAttemptCommand(Guid ChartId,
    bool IsBroken, PhoenixScore? Score, PhoenixPlate? Plate, bool KeepBestStats = false,
    string Source = ScoreJournalEntry.ManualSource, MixEnum Mix = MixEnum.Phoenix,
    Guid? SessionId = null,
    // When the official site supplied the save time (the redesigned my_page stamps every
    // card), it becomes the record's date and the journal's OccurredAt; null = clock time.
    DateTimeOffset? RecordedAt = null,
    JudgementCounts? Judgements = null) : IRequest
{
}
