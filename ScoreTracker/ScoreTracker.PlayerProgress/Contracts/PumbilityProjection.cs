using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     What the PUMBILITY estimator produced for one player in one mix
///     (docs/design/pumbility-overhaul.md §4.1). Charts with no peer coverage are simply
///     absent — an absent chart means "no opinion", never zero.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PumbilityProjection(
    IReadOnlyDictionary<Guid, PhoenixScore> ExpectedScores,
    IReadOnlyDictionary<Guid, int> ProjectedGains,
    IReadOnlyDictionary<Guid, TierListCategory> ChartDifficulty,
    IReadOnlyDictionary<Guid, ProjectionEvidence> Evidence);

/// <summary>
///     What an estimate was built from, so the page can say how much it heard rather than
///     claiming to know why.
///     <para>
///         This replaces the per-skill "why" the old projection carried. The estimator has no
///         skill term — four ways of adding one each measured at or under 0.3% (§4.3) — so a
///         per-skill attribution beside a projection would assert a causal path that does not
///         exist. How many peers spoke, how current they were, and how much they disagreed is
///         both true and useful.
///     </para>
/// </summary>
/// <param name="PeerCount">Peers inside the ±1 competitive band who have played the chart.</param>
/// <param name="EffectivePeers">
///     Summed growth weights — voices, not heads. Ten peers who have all levelled well past the
///     score they lent are worth about one.
/// </param>
/// <param name="Spread">
///     Points between the 10th and 90th percentile of those peers' scores. A wide spread means
///     the estimate is a guess between very different outcomes.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record ProjectionEvidence(int PeerCount, double EffectivePeers, int Spread);
