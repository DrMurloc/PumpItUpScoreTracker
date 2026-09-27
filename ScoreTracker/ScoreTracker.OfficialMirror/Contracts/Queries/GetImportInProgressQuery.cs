using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     Whether the player has an official import running on this mix right now — from the press to the
///     run's end. While one is, every other way of saving a score on that mix refuses: the score form, the
///     CSV upload, the v1 record API and the v2 plays API (docs/design/import-restart-recovery.md §0).
///     Asked where each save starts rather than inside the Ledger, so the import's own saves pass.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetImportInProgressQuery(Guid UserId, MixEnum Mix) : IQuery<bool>;
