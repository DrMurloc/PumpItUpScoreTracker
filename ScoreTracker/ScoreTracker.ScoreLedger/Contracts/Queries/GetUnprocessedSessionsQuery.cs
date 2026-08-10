using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     Sessions whose scores landed but whose derived work never ran — the cheap end to start a
///     restart-recovery pass from (docs/design/import-restart-recovery.md §3.1).
///     <para>
///         Deliberately unfiltered by time or user. The set is tiny by construction: every session
///         predating the marker is backfilled as processed, and a live session is stamped the
///         moment its first batch finishes capturing. Asking the other way round — every import
///         run older than the batch window, then "is it processed?" — cannot be one query, because
///         the run and the marker live in different verticals.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetUnprocessedSessionsQuery : IQuery<IReadOnlyList<ScoreSessionRecord>>;
