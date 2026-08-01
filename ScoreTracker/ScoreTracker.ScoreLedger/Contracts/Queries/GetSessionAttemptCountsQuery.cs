using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     How many times each chart was played in this session before the play that cleared it.
///     Session-local on purpose: the journal only holds losing attempts from 2026-07-30 on, and
///     only as deep as the official site's single recently-played page reached at import time —
///     so "your seventh try tonight" is defensible where an all-time count would not be.
///     <para>
///         A chart is absent from the result when it never cleared in this session, or cleared
///         on the first play. The caller renders nothing in either case
///         (docs/design/session-breakdown.md D5).
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetSessionAttemptCountsQuery(Guid UserId, Guid SessionId, IReadOnlyList<Guid> ChartIds)
    : IQuery<IReadOnlyDictionary<Guid, int>>;
