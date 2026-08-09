using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     What the PUMBILITY estimator produced for one player in one mix
///     (docs/design/pumbility-overhaul.md §4.1). Charts with no peer coverage are simply
///     absent — an absent chart means "no opinion", never zero.
///     <para>
///         It carries no account of what the estimate was built from. Peer counts and spreads
///         were printed beside every projection for a while and told a player nothing they
///         could act on — the number is the best available estimate either way, and a thin
///         cohort is a reason to gate a suggestion, not to caption it (owner, 2026-08-07).
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PumbilityProjection(
    IReadOnlyDictionary<Guid, PhoenixScore> ExpectedScores,
    IReadOnlyDictionary<Guid, double> ProjectedGains,
    IReadOnlyDictionary<Guid, TierListCategory> ChartDifficulty);
