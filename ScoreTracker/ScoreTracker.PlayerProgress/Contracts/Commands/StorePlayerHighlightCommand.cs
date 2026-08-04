using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Commands;

/// <summary>
///     Writes one already-classified set of wins into the ledger. Idempotent on the event id, so
///     a re-run is a no-op rather than a duplicate.
///     <para>
///         Exists for the backfill, which reads payloads Communities wrote before the move and
///         has to hand them somewhere. It deliberately does NOT publish
///         <c>PlayerHighlightsStoredEvent</c>: the audience index rows for those events already
///         exist — they are the very rows the backfill is reading from.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record StorePlayerHighlightCommand(
    Guid EventId,
    Guid UserId,
    MixEnum Mix,
    DateTimeOffset OccurredAt,
    Guid? SessionId,
    IReadOnlyList<SignificantWin> Wins) : IRequest<bool>;
