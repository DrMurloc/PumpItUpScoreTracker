using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

/// <summary>
///     Records a best on XX or older. A null <paramref name="LetterGrade" /> deletes the record —
///     the manual form's way of clearing one.
/// </summary>
/// <param name="KeepBestStats">
///     Apply <see cref="LegacyBestAttemptPolicy" /> rather than overwriting. False is the manual
///     routes, where the player is the authority and a correction must be able to lower a
///     record. True is every acquisition source — a weekly-board entry, an import — which may
///     only ever raise one, and raises the two axes independently.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record UpdateXXBestAttemptCommand(Guid chartId,
    XXLetterGrade? LetterGrade, bool IsBroken, XXScore? Score, MixEnum Mix = MixEnum.XX,
    bool KeepBestStats = false,
    string Source = ScoreJournalEntry.ManualSource) : IRequest
{
}
