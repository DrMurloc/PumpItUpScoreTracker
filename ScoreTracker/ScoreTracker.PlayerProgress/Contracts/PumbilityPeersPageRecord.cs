using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     The Phoenix 2 Play page (docs/design/pumbility-overhaul.md §3.10): what the viewer's
///     PUMBILITY peers hold, tiered by prevalence, with the peers' score statistics and the
///     viewer's own standing on every chart; the charts only the viewer holds; and the peers
///     themselves. Everything here is per chart type and scoped by <paramref name="Pool" /> —
///     null merges both types into one list whose tiers were computed per type (D37).
/// </summary>
/// <param name="Peers">
///     The peer group per chart type in scope — the same record the projection's chips print
///     (D27, D28). A type whose pool is short is present here and dark, and absent everywhere
///     else in the record.
/// </param>
/// <param name="Entries">
///     Every chart at least one peer holds, for every lit type in scope. Unordered beyond what
///     <see cref="PeerPoolEntry.Order" /> says within a type; the page groups and sorts.
/// </param>
/// <param name="YoursAlone">The viewer's own pool charts that no peer holds — the other half of the overlay.</param>
/// <param name="Roster">The public peers, strongest total first. Private peers are counted, never listed.</param>
/// <param name="PrivatePeers">How many peers are private accounts, so the page can say they exist.</param>
/// <param name="You">The viewer's own row in the roster's terms, for the page to place among the peers.</param>
/// <param name="Compare">Per lit type, how the viewer's pool differs from what the peers hold (D41).</param>
[ExcludeFromCodeCoverage]
public sealed record PumbilityPeersPageRecord(
    MixEnum Mix,
    ChartType? Pool,
    IReadOnlyDictionary<ChartType, PeerGroup> Peers,
    IReadOnlyList<PeerPoolEntry> Entries,
    IReadOnlyList<PeerAloneEntry> YoursAlone,
    IReadOnlyList<PeerRosterEntry> Roster,
    int PrivatePeers,
    PeerRosterEntry? You,
    IReadOnlyDictionary<ChartType, PeerCompare> Compare)
{
    /// <summary>The empty answer: no peers, nothing held, nobody to list.</summary>
    public static PumbilityPeersPageRecord Empty(MixEnum mix, ChartType? pool)
    {
        return new PumbilityPeersPageRecord(mix, pool, new Dictionary<ChartType, PeerGroup>(),
            Array.Empty<PeerPoolEntry>(), Array.Empty<PeerAloneEntry>(), Array.Empty<PeerRosterEntry>(), 0, null,
            new Dictionary<ChartType, PeerCompare>());
    }
}

/// <summary>
///     One chart the peers hold, and how the viewer stands on it.
/// </summary>
/// <param name="Holders">Peers holding it in their pool of the type.</param>
/// <param name="PeerCount">Peers of the type — the count's denominator, so a card can say "5 of 7".</param>
/// <param name="Points">Its prevalence, the slot-weighted sum (D33) — the tooltip's "Weighted sum".</param>
/// <param name="Tier">Staple … Poor, banded per type over the weighted sums with the PUMBILITY lens's log rule.</param>
/// <param name="Order">The banding's own order within the type, ascending — a stabiliser, not a rank.</param>
/// <param name="Scored">Peers with a score on it, holders or not.</param>
/// <param name="Median">The peers' median, or null under five scorers (D24).</param>
/// <param name="Variability">How split the peers are, or null under five scorers (D35).</param>
/// <param name="MyPoolRank">The chart's slot in the viewer's own pool of the type, or null when it holds no slot.</param>
/// <param name="MyScore">The viewer's own non-broken score, or null when they have none.</param>
/// <param name="MyPlate">The plate on that score, when it carries one.</param>
/// <param name="MyPercentile">The share of scorers the viewer's score beats, on 0..1; null without a score or scorers.</param>
[ExcludeFromCodeCoverage]
public sealed record PeerPoolEntry(
    Guid ChartId,
    ChartType ChartType,
    int Holders,
    int PeerCount,
    int Points,
    TierListCategory Tier,
    int Order,
    int Scored,
    PhoenixScore? Median,
    PhoenixScore? Quartile1,
    PhoenixScore? Quartile3,
    PeerVariabilityLevel? Variability,
    int? MyPoolRank,
    PhoenixScore? MyScore,
    PhoenixPlate? MyPlate,
    double? MyPercentile)
{
    /// <summary>The chart's share of its electorate's points, on 0..1 — comparable across types (D37).</summary>
    public double Share => PeerCount == 0 ? 0 : Points / (PeerCount * (double)PoolVote);

    /// <summary>What one peer's whole pool is worth in points: 50 + 49 + … + 1.</summary>
    public const int PoolVote = PumbilityPeerPools.PoolSize * (PumbilityPeerPools.PoolSize + 1) / 2;
}

/// <summary>A chart in the viewer's own pool that no peer holds.</summary>
[ExcludeFromCodeCoverage]
public sealed record PeerAloneEntry(Guid ChartId, ChartType ChartType, int MyPoolRank, PhoenixScore Score,
    PhoenixPlate? Plate, double Value);

/// <summary>
///     One row of the roster: who, their level and total, their competitive levels, which types
///     they are a peer for, and how many of the viewer's pool charts of each type they also hold.
///     <paramref name="RungIndex" /> is null where the mix has no PUMBILITY ladder to read a gem
///     from (Phoenix 1, D43).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PeerRosterEntry(
    User User,
    double Total,
    int? RungIndex,
    double SinglesLevel,
    double DoublesLevel,
    IReadOnlySet<ChartType> PeerFor,
    IReadOnlyDictionary<ChartType, int> Overlap);

/// <summary>
///     How the viewer's pool of one type differs from what the peers hold (D41).
/// </summary>
/// <param name="InCommon">Viewer pool charts inside the peers' fifty most prevalent.</param>
/// <param name="HeldByAtMostOne">Viewer pool charts held by one peer or none.</param>
/// <param name="Alone">Viewer pool charts held by no peer.</param>
/// <param name="MyLevels">The viewer's pool charts per level.</param>
/// <param name="PeerShareByLevel">The peers' prevalence points per level, as a share of the type's total.</param>
[ExcludeFromCodeCoverage]
public sealed record PeerCompare(
    int InCommon,
    int HeldByAtMostOne,
    int Alone,
    IReadOnlyDictionary<int, int> MyLevels,
    IReadOnlyDictionary<int, double> PeerShareByLevel);
