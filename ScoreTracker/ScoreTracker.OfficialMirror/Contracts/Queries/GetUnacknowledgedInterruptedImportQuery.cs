using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     The newest interrupted run this player has not yet been shown a notice for, or null.
///     Drives the one-time dialog (docs/design/import-restart-recovery.md §7).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetUnacknowledgedInterruptedImportQuery(Guid UserId) : IQuery<ImportAttemptRecord?>;
