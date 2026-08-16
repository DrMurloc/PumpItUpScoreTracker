using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     What the PUMBILITY estimator produced for one player in one mix
///     (docs/design/pumbility-overhaul.md §4.1 on Phoenix 1, §4.8 on Phoenix 2). Charts with no
///     peer coverage are simply absent — an absent chart means "no opinion", never zero.
///     <para>
///         No row carries an account of what its estimate was built from. Peer counts and
///         spreads were printed beside every projection for a while and told a player nothing
///         they could act on — the number is the best available estimate either way, and a thin
///         peer group is a reason to gate a suggestion, not to caption it (owner, 2026-08-07).
///         What IS carried is the peer group per chart type (<paramref name="Peers" />): who was
///         asked, how many there are, and — on Phoenix 2 — whether the viewer's own pool of the
///         type is deep enough for the group to exist (D27, D28). One line for the section, not
///         a caption per row.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PumbilityProjection(
    IReadOnlyDictionary<Guid, PhoenixScore> ExpectedScores,
    IReadOnlyDictionary<Guid, double> ProjectedGains,
    IReadOnlyDictionary<Guid, TierListCategory> ChartDifficulty,
    IReadOnlyDictionary<ChartType, PeerGroup>? Peers = null);
