using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Messages;

/// <summary>
///     One-shot cutover press: builds a sealed baseline snapshot from the legacy
///     UserOfficialLeaderboard rows (priming the record books, emitting no highlights) so
///     the hub has data before the first new-pipeline sweep. No-ops once any sealed
///     snapshot exists for the mix.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SeedBaselineSnapshotCommand(MixEnum Mix);
