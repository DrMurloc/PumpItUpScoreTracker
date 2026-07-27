using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Messages;

/// <summary>
///     ONE-TIME ADMIN PRESS — delete this and its button once it has been run.
///     Re-scrapes the mix's rating boards alone and re-attaches them to the latest sealed
///     snapshot: minutes instead of a full board sweep, and no new snapshot, so the rows
///     belong to the run that already happened rather than inventing a second one. It
///     exists to repair two stages that shipped broken — Phoenix never mirrored its
///     PUMBILITY board at all, and Phoenix 2's avatars all landed as the default — on the
///     current week without waiting for the next sweep. Chart placements, popularity,
///     highlights and the seal are untouched. Ordinary weekly imports need none of this.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RefreshRatingBoardsCommand(MixEnum Mix);
