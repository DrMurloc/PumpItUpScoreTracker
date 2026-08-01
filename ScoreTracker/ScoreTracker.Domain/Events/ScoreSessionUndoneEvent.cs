using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Events;

/// <summary>
///     A session was undone and the Ledger has rebuilt the charts it touched. Progression-side
///     records that session produced — highlights, milestones — are PlayerProgress's to remove;
///     neither is recomputed, so leaving them would strand a "you reached Expert" card for a
///     title the player no longer holds.
///     It lives in Domain rather than the Ledger's contracts because PlayerProgress sits below
///     ScoreLedger in the reference graph and cannot see up.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ScoreSessionUndoneEvent(Guid UserId, Guid SessionId, MixEnum Mix);
