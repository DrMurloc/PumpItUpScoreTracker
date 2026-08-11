using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

/// <summary>
///     Withdraws the player's broken records on the chosen mixes: every chart whose record is a
///     failed run goes back to having no record, exactly as if they had never opted into recording
///     one. Returns how many were removed.
///     <para>
///         Narrower than <see cref="WipeUserScoresCommand" /> in kind, not just in scope — the
///         journal is deliberately untouched, so the runs stay in each chart's history and only
///         their standing as the record is withdrawn. That is also what makes this re-derivable:
///         turning the setting back on and importing again brings them back, because the official
///         site still lists them.
///     </para>
///     <paramref name="Mixes" /> is always explicit, for the same reason the wipe's is: there is
///     no "null means everything" to forget to set.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DeleteBrokenRecordsCommand(
    Guid UserId,
    IReadOnlyCollection<MixEnum> Mixes) : IRequest<int>;
