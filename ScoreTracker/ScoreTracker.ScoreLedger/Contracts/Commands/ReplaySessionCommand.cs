using MediatR;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

/// <summary>
///     Rebuilds one session's lost score batch from the journal and republishes it, so everything
///     downstream — highlights, folder lamps, ratings, titles, the personalized tier list, the
///     session card — runs after all (docs/design/import-restart-recovery.md).
///     <para>
///         Safe to send for a session that does not need it: the handler re-checks the processed
///         marker and does nothing. Returns how many changes it announced, which is 0 for both
///         "already done" and "nothing to announce".
///     </para>
///     <para>
///         <paramref name="Announce" /> false replays everything but the Discord card — a sitting
///         closed more than a day after its last play.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ReplaySessionCommand(Guid UserId, Guid SessionId, bool Announce = true) : IRequest<int>;
