using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Messages;

/// <summary>
///     Finish a failed run whose download completed: work out its week and publish it, dated to
///     when the download ended. The admin page's runs table sends it for a run it marks finishable.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record FinishLeaderboardRunCommand(MixEnum Mix, int SnapshotId);
