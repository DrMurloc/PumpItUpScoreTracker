using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Messages;

/// <summary>
///     Rebuilds the supplemented reading of a mix's latest sealed snapshot: the ledger bests
///     of linked public players, merged into every board, plus the supplemented half of This
///     Week. Official rows, the record books and the seal are never touched.
///     <para>
///         Two triggers, one path. The weekly sweep publishes it after each seal, and the
///         admin page publishes it on demand — which is how the feature goes live without
///         waiting for a Sunday, and how a bad roll-up is simply re-run.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RollUpSupplementedLeaderboardsCommand(MixEnum Mix);
