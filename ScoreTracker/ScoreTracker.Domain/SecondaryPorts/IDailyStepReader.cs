using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     Daily Step's published read (ADR-001 D3 "pull"): the live daily chart id(s) for a mix.
///     Lets the official-import ecosystem notice a recent play that landed on today's daily board
///     — and emit its lowest-passing score for Limbo Day — without reaching into the
///     WeeklyChallenge internals. 0–1 ids per mix today; returns a set to stay future-proof.
/// </summary>
public interface IDailyStepReader
{
    Task<IEnumerable<Guid>> GetCurrentChartIds(MixEnum mix, CancellationToken cancellationToken = default);
}
