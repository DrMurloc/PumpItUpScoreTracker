using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>
///     Reads the hand-maintained set of charts carrying a limbo leaderboard
///     (docs/design/limbo-leaderboard.md D1). Read-only by design: rows arrive by SQL, so there is
///     no write method here to tempt anyone into building the admin screen this deliberately
///     does not have.
/// </summary>
internal interface ILimboChartRepository
{
    Task<IReadOnlySet<Guid>> GetLimboCharts(MixEnum mix, CancellationToken cancellationToken);
}
