using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

/// <summary>
///     Plays the acquisition side saw that did NOT become a record — the official site's
///     recently-played list. They land in the journal only; the ledger's record is untouched
///     (docs/design/score-truth-model.md D5). Idempotent: <see cref="ObservedPlay.PlayedAt" />
///     is the site's own play time, so re-importing the same window inserts nothing.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RecordObservedPlaysCommand(
    Guid UserId,
    MixEnum Mix,
    string Source,
    Guid? SessionId,
    IReadOnlyList<RecordObservedPlaysCommand.ObservedPlay> Plays) : IRequest
{
    [ExcludeFromCodeCoverage]
    public sealed record ObservedPlay(
        Guid ChartId,
        PhoenixScore Score,
        PhoenixPlate? Plate,
        bool IsBroken,
        DateTimeOffset PlayedAt,
        JudgementCounts? Judgements);
}
