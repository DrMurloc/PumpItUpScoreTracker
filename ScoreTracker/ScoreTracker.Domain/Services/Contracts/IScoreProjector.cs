using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Services.Contracts;

/// <summary>
///     One chart to project, with the level its peers are read at. The level travels with the
///     id because the projector never reads the catalog — on Phoenix 1 it asks the score store
///     for a level band rather than a list of several hundred chart ids. Phoenix 2 reads every
///     record of the type regardless (docs/design/pumbility-overhaul.md §4.8), so there the
///     level is carried and unused.
/// </summary>
public readonly record struct ProjectionTarget(Guid ChartId, int Level);

/// <summary>What a projection run is asked for.</summary>
/// <param name="CompetitiveWindow">
///     Competitive-level half-width of the peer gate on Phoenix 1. Deliberately required rather
///     than defaulted: PUMBILITY wants ±1.0 because it quotes the projected number and that
///     window is measured optimal for accuracy, while a tier list wants ±0.5 because it only
///     ranks charts against each other and the rest of the site calls a competitive peer ±0.5. A
///     default here would let one of them drift onto the other's answer silently. Ignored on
///     Phoenix 2, whose peers are drawn on the PUMBILITY ladder and not on a level at all.
/// </param>
/// <param name="Charts">
///     The mix's catalog, keyed by chart id, for a caller that also wants the peers' pools
///     (<see cref="ScoreProjection.PeerPools" />). Pricing a record needs the chart, and the
///     projector reads no catalog of its own; without this the Phoenix 2 run still estimates and
///     simply returns no pools. Ignored on Phoenix 1, whose peers hold no PUMBILITY pool.
/// </param>
/// <param name="RelaxFloorWhenEmpty">
///     Phoenix 2 only: when the five-peer floor (<see cref="PeerEstimator.Phoenix2MinimumPeers" />)
///     leaves the run with nothing at all, read the same records again with no floor rather than
///     answer empty (D47). Opt-in per caller, because the two consumers want different things from
///     an unanswerable question: a push list would rather name a chart on one peer's evidence than
///     show a player an empty board, while the personalized tier list falls back to the community
///     list, which is a better answer than a folder ranked on single scores. Nothing here changes
///     what a full band produces — the second pass runs only from zero.
/// </param>
/// <param name="ProjectedTotal">
///     Phoenix 2 only: the PUMBILITY total to place the viewer on the rung ladder by — their real
///     one where their merged pool is full, and where they would finish at their current average
///     where it is not (D48). Supplying it is also what lowers the pool gate from
///     <see cref="PeerGroup.PumbilityPoolSize" /> to <see cref="PeerGroup.PumbilityProjectionGate" />,
///     because the two go together: a short pool's own total is the sum of the charts it happens to
///     hold, which would seat a strong player at the bottom of the ladder among peers who tell them
///     nothing. A caller with no answer to "where will they finish" leaves this null and keeps the
///     full-pool gate. Ignored on Phoenix 1, which seats nobody on a ladder.
/// </param>
/// <param name="ProjectedTotalIsEstimate">
///     Whether <paramref name="ProjectedTotal" /> was extrapolated or is the player's settled
///     number. Only the caller knows: a full MERGED pool yields a real total even while the type
///     being viewed holds twenty-odd charts, and a surface that told that player their peers came
///     from an estimate would be describing something that did not happen. Rides out on
///     <see cref="PeerGroup.PlacedByEstimate" />.
/// </param>
/// <param name="Quantiles">
///     The rungs to read the peers at — every one comes back per chart on
///     <see cref="ScoreProjection.Ladders" />, and the first is what <see cref="ScoreProjection.Scores" />
///     prints. Null or empty is the one default rung, <see cref="PeerEstimator.DefaultQuantile" />,
///     which is what every surface reads unless it lets the player choose (D50, D51). A caller that
///     caches the run asks for every rung it might later be asked for, so a change of rung is a
///     lookup and never a second sweep.
/// </param>
public sealed record ScoreProjectionRequest(
    MixEnum Mix,
    ChartType ChartType,
    Guid UserId,
    IReadOnlyCollection<ProjectionTarget> Targets,
    double CompetitiveWindow,
    IReadOnlyDictionary<Guid, Chart>? Charts = null,
    bool RelaxFloorWhenEmpty = false,
    double? ProjectedTotal = null,
    bool ProjectedTotalIsEstimate = false,
    IReadOnlyCollection<double>? Quantiles = null)
{
    /// <summary>The rungs actually read: what was asked for, or the default alone.</summary>
    public IReadOnlyCollection<double> Rungs =>
        Quantiles is { Count: > 0 } ? Quantiles : new[] { PeerEstimator.DefaultQuantile };

    /// <summary>The rung <see cref="ScoreProjection.Scores" /> is read at — the first one asked for.</summary>
    public double PrimaryQuantile => Rungs.First();
}

/// <summary>How a peer group was drawn — the two definitions the site has.</summary>
public enum PeerGroupKind
{
    /// <summary>
    ///     Phoenix 1: players within a competitive-level band of the viewer — the viewer out,
    ///     nobody gated on a pool. Since round six the page's peers too (D43), so the group's
    ///     size is the band's size, not the number whose evidence reached an estimate.
    /// </summary>
    CompetitiveBand,

    /// <summary>
    ///     Phoenix 2: PUMBILITY peers — players within a rung band of the viewer on the
    ///     PUMBILITY level ladder who hold a full pool of the chart type
    ///     (docs/design/pumbility-overhaul.md §4.8, D22).
    /// </summary>
    PumbilityBand
}

/// <summary>
///     Who an estimate was drawn from, named so a surface can say it without knowing the mix:
///     the kind of band, where it is centred and how wide, how many players are in it, and —
///     on Phoenix 2 — whether the viewer's own pool of the type is deep enough for the group to
///     exist at all.
/// </summary>
/// <param name="Center">
///     A competitive level on a competitive band; a rung index (0–36) on a PUMBILITY band.
/// </param>
/// <param name="HalfWidth">Levels on a competitive band; rungs on a PUMBILITY band.</param>
/// <param name="Size">
///     Players in the group, the viewer excluded — a player is never one of their own peers. On
///     a competitive band this is the number whose scores actually reached an estimate — most of
///     a level band has played none of the charts asked about, so the honest figure is the one
///     that voted. On a PUMBILITY band it is the group itself: every player meeting the
///     definition, whether or not they played the charts asked about, because that group is a
///     thing the page names. Zero for a Phoenix 2 viewer whose own pool of the type is short: the
///     band is not counted for a viewer it cannot yet serve, and the page prints the pool instead.
/// </param>
/// <param name="PoolCount">
///     The viewer's rated charts of the type, capped at <paramref name="PoolSize" />. Zero on a
///     competitive band, which has no such gate.
/// </param>
/// <param name="PoolSize">Fifty on a PUMBILITY band (D28); zero on a competitive band.</param>
/// <param name="PlacedByEstimate">
///     Whether <paramref name="Center" /> came from an extrapolated finish rather than the viewer's
///     settled total (D48). A short pool of ONE TYPE does not imply it: a player with a full merged
///     pool and twenty-odd doubles is placed by a real number, and a surface must not tell them
///     otherwise. False on a competitive band, which places nobody on a ladder.
/// </param>
/// <param name="AnsweredBelowFloor">
///     Whether this run fell back below the five-peer floor (D47) and produced rows from it —
///     every score in the projection then rests on FEWER than five peers, because a single chart
///     meeting the floor is what would have stopped the fallback. This, not the band's size, is
///     what a surface warns on: a band of nine whose charts were each scored by two or three
///     relaxes exactly as a band of two does, and a warning keyed off size would miss it.
///     False when the strict floor answered, and on a competitive band, which has no floor.
/// </param>
public sealed record PeerGroup(PeerGroupKind Kind, double Center, double HalfWidth, int Size, int PoolCount,
    int PoolSize, bool PlacedByEstimate = false, bool AnsweredBelowFloor = false)
{
    /// <summary>The rung half-width of a Phoenix 2 peer group: DIAMOND LV.4 reaches DIAMOND LV.1 and RED BERYL LV.2.</summary>
    public const int PumbilityRungWindow = 3;

    /// <summary>The pool a Phoenix 2 viewer, and each of their peers, must hold of the chart type.</summary>
    public const int PumbilityPoolSize = 50;

    /// <summary>
    ///     The shorter pool a viewer may be projected from when the caller can say where they will
    ///     finish (D48). A peer still needs a full <see cref="PumbilityPoolSize" /> — their pool is
    ///     the evidence, and half a pool is half a vote — but the viewer only has to be placeable,
    ///     and twenty charts places them: backtested across 111 full-pool Phoenix 2 accounts, their
    ///     top twenty with the remaining thirty slots filled at their weakest held chart lands on
    ///     the exact rung 39 times, within two rungs 110 times, and within three 110 times. What
    ///     is left is one-directional — the estimate reads high, never low — so the ±3 band is not
    ///     symmetric around the truth: at a twenty-chart pool it reaches roughly [−2, +4] rather
    ///     than [−3, +3]. That is the accepted cost of placing a short pool at all; a caller who
    ///     cannot afford it supplies no finish and keeps the full-pool gate.
    /// </summary>
    public const int PumbilityProjectionGate = 20;

    /// <summary>
    ///     The lowest level a Phoenix 2 chart prices above zero at — the pool's floor, so a
    ///     player's non-broken records of the type from here up ARE their pool, and
    ///     <see cref="PumbilityPoolSize" /> of them is a full one. One constant, because the
    ///     projection, the tier lists' PUMBILITY lens and the nightly list builder all count the
    ///     same pool and must agree on where it starts.
    /// </summary>
    public static readonly DifficultyLevel PumbilityPoolFloor = DifficultyLevel.From(10);

    /// <summary>
    ///     The rung indices a Phoenix 2 peer band spans around <paramref name="rungIndex" />:
    ///     <see cref="PumbilityRungWindow" /> either side, clipped to the ladder (nothing lies
    ///     below index 0 or above the capstone). The one place the clip lives — the projector
    ///     draws its peers with it, the tier-list builder keys its lists with it, and a page that
    ///     names the band prints it.
    /// </summary>
    public static (int Lowest, int Highest) PumbilityBand(int rungIndex)
    {
        return (Math.Max(0, rungIndex - PumbilityRungWindow),
            Math.Min(Phoenix2PumbilityLevel.CapstoneIndex, rungIndex + PumbilityRungWindow));
    }

    /// <summary>
    ///     False only for a Phoenix 2 viewer whose pool of the type is short of the gate its run
    ///     was made under: the group is defined but the viewer is not yet in a position to have
    ///     one, and the surface says so instead of estimating.
    /// </summary>
    public bool IsLit => PoolSize == 0 || PoolCount >= PoolSize;

    public static PeerGroup Competitive(double level, double window, int size)
    {
        return new PeerGroup(PeerGroupKind.CompetitiveBand, level, window, size, 0, 0);
    }

    /// <summary>
    ///     A PUMBILITY band. <paramref name="poolSize" /> is the gate the run was made under, so a
    ///     surface printing "N of M charts" names the number that would actually light this viewer
    ///     up — twenty where the caller supplied a projected finish, fifty otherwise (D48).
    /// </summary>
    public static PeerGroup Pumbility(int rungIndex, int size, int poolCount,
        int poolSize = PumbilityPoolSize, bool placedByEstimate = false)
    {
        // Counted against the FULL pool rather than the gate, so a lit-but-short viewer still
        // knows how many of the fifty they hold — which is what the note that explains their
        // projection has to say (D48). The cap only ever bites above fifty, where the viewer is
        // lit under either gate and no surface prints the count anyway.
        return new PeerGroup(PeerGroupKind.PumbilityBand, rungIndex, PumbilityRungWindow, size,
            Math.Min(poolCount, PumbilityPoolSize), poolSize, placedByEstimate);
    }
}

/// <summary>
///     The peers' scores on one chart read at several quantiles — the same voices, the same
///     growth weights, the same arithmetic at every rung — and how many peers voted. A caller
///     asks for the rungs it will read (<see cref="ScoreProjectionRequest.Quantiles" />) and gets
///     exactly those; <see cref="At" /> answers a requested rung exactly and interpolates
///     between the two nearest for anything else, so a rung nobody asked for is never invented
///     from nothing. This is what a surface that lets the player choose a rung caches (D51).
/// </summary>
public sealed record PeerLadder(IReadOnlyDictionary<double, PhoenixScore> Rungs, int PeerCount)
{
    /// <summary>The score at a rung: exact where the rung was asked for, linear between its neighbours otherwise.</summary>
    public PhoenixScore At(double quantile)
    {
        if (Rungs.TryGetValue(quantile, out var exact)) return exact;
        var ordered = Rungs.OrderBy(kv => kv.Key).ToArray();
        if (quantile <= ordered[0].Key) return ordered[0].Value;
        if (quantile >= ordered[^1].Key) return ordered[^1].Value;
        for (var i = 1; i < ordered.Length; i++)
        {
            if (quantile > ordered[i].Key) continue;
            var (lowQuantile, low) = ordered[i - 1];
            var (highQuantile, high) = ordered[i];
            var t = (quantile - lowQuantile) / (highQuantile - lowQuantile);
            return PhoenixScore.From((int)Math.Round((int)low + t * ((int)high - (int)low)));
        }

        return ordered[^1].Value;
    }
}

/// <summary>
///     One chart as the peers' pools and scores see it (docs/design/pumbility-overhaul.md §3.10).
/// </summary>
/// <param name="Holders">Peers holding the chart in their top-50 pool of the type.</param>
/// <param name="Points">
///     Its prevalence — a peer's #1 chart contributes 50, their #50 contributes 1, summed over
///     the holders (a Borda count, D33). Every peer casts the same 1,275 points, which is what
///     keeps a strong peer from outvoting a weak one. Zero when nobody holds it.
/// </param>
/// <param name="Scored">Peers with a non-broken score on it, holders or not.</param>
/// <param name="Scores">
///     Every scorer's score, ascending — the voices a projected grade is read from
///     (<see cref="ProjectedAt" />), kept so a page can read any rung and place the viewer's own
///     score among them without a second read.
/// </param>
public sealed record PeerPoolChart(int Holders, int Points, int Scored, IReadOnlyList<int> Scores)
{
    /// <summary>The share of scorers strictly below <paramref name="score" />, on 0..1; null with no scorers.</summary>
    public double? PercentileOf(int score)
    {
        return Scores.Count == 0 ? null : Scores.Count(s => s < score) / (double)Scores.Count;
    }

    /// <summary>
    ///     What a player is projected to score here at a rung — the peers' scores at that quantile,
    ///     every voice at full weight, with the estimator's own arithmetic and its five-scorer
    ///     floor (D24): null means no opinion. Read on demand from the voices rather than stored
    ///     per rung, so a page that lets the player choose the rung (D51) asks and is answered.
    /// </summary>
    public PhoenixScore? ProjectedAt(double quantile)
    {
        var estimate = PeerEstimator.Estimate(Scores.Select(score => new PeerScore(score, 0, 0)).ToArray(), 0,
            quantile, PeerEstimator.Phoenix2MinimumPeers);
        return estimate == null ? null : PhoenixScore.From(estimate.Value);
    }
}

/// <summary>
///     What a Phoenix 2 peer group's pools are made of: who the peers are, what each of them
///     holds, and every chart any of them holds or five of them scored, as
///     <see cref="PeerPoolChart" />. Read from the same records the estimate is, with the viewer
///     removed (D31), so a page listing "what players like me build their number from" and the
///     projection beside it cannot disagree about who those players are.
/// </summary>
/// <param name="PeerIds">The peers — every player in the band holding a full pool of the type.</param>
/// <param name="Pools">Each peer's top-50 chart set, keyed by peer.</param>
/// <param name="Charts">Every chart at least one peer holds, or at least five scored.</param>
public sealed record PeerPoolSummary(
    IReadOnlySet<Guid> PeerIds,
    IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> Pools,
    IReadOnlyDictionary<Guid, PeerPoolChart> Charts);

/// <summary>
///     What a projection run produced, and the peers it produced it from.
/// </summary>
/// <param name="Scores">
///     The projected score per chart. A chart no peer has played is simply absent — absent means
///     "no opinion", never zero, and callers must render it as such.
/// </param>
/// <param name="PeerCount">
///     How many distinct players' scores are actually behind <paramref name="Scores" /> — not how
///     many sit in the band, most of whom have played none of these charts. A surface quoting how
///     many players voted has to quote the ones that did, or the figure overstates the evidence
///     by roughly the share of the band that skipped the folder. The group's own size is on
///     <paramref name="Group" />.
/// </param>
/// <param name="CompetitiveLevel">
///     The level the peers were matched around on Phoenix 1, so a caller can state the band it
///     read without resolving the player's level a second way. On Phoenix 2 the peers are not
///     matched on a level; this carries the viewer's own competitive level of the type for
///     callers that still show one, and <paramref name="Group" /> is what describes the band.
/// </param>
/// <param name="MeanFreshness">
///     Mean growth weight across the scores that contributed, on 0..1: 1.0 means every peer has
///     the level they had when they set their score, and lower means the group has outgrown its
///     own evidence. Zero when nothing contributed. Always 1.0 on Phoenix 2, which weighs nothing.
/// </param>
/// <param name="Group">Who was asked, in a form a surface can name. Null only where nothing was.</param>
/// <param name="PeerPools">
///     The peers' pools, on Phoenix 2 and only when the request carried the catalog
///     (<see cref="ScoreProjectionRequest.Charts" />); null otherwise, which means "not asked",
///     never "nobody holds anything".
/// </param>
/// <param name="Ladders">
///     Per chart in <paramref name="Scores" />, the peers read at every rung the request asked
///     for (<see cref="ScoreProjectionRequest.Quantiles" />) — same voices, same weights, same
///     arithmetic — so a caller that caches the run can answer any of those rungs later without
///     reading a peer again. <paramref name="Scores" /> is the first rung of each.
/// </param>
public sealed record ScoreProjection(
    IReadOnlyDictionary<Guid, PhoenixScore> Scores,
    int PeerCount,
    double CompetitiveLevel,
    double MeanFreshness,
    PeerGroup? Group = null,
    PeerPoolSummary? PeerPools = null,
    IReadOnlyDictionary<Guid, PeerLadder>? Ladders = null)
{
    /// <summary>No opinion, for the runs that stop before a peer group exists.</summary>
    public static ScoreProjection None(double competitiveLevel = 0, PeerGroup? group = null)
    {
        return new ScoreProjection(new Dictionary<Guid, PhoenixScore>(), 0, competitiveLevel, 0, group);
    }
}

/// <summary>
///     "What would this player score on this chart" — the one implementation of that question
///     (docs/design/pumbility-overhaul.md §4.1 on Phoenix 1, §4.8 on Phoenix 2), shared so the
///     PUMBILITY projection and the personalized Score tier list cannot answer it differently.
///     <para>
///         It answers only that. Which charts are worth asking about is the caller's business:
///         PUMBILITY asks about unplayed charts that could clear its pool bar, the tier list
///         asks about every chart in the folder being viewed.
///     </para>
/// </summary>
public interface IScoreProjector
{
    /// <summary>
    ///     The projected scores, and the peers behind them. The peer figures ride along because
    ///     the run already computes them and a second pass to recover them would re-read every
    ///     peer's scores — a surface that wants to say what its number rests on should not have to
    ///     pay for the sweep twice.
    /// </summary>
    Task<ScoreProjection> Project(ScoreProjectionRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    ///     The level this projection matches peers on, on Phoenix 1. Published because a caller
    ///     narrowing the charts it asks about has to narrow them around the same number the peers
    ///     are drawn around — two readings of "what level is this player" would let the scope and
    ///     the peers disagree. Phoenix 2 draws its peers on the PUMBILITY ladder and its callers
    ///     scope on nothing (D24), so they have no reason to ask; if one does, it gets the
    ///     player's competitive level in that mix and nothing borrowed from another.
    ///     <para>
    ///         1 is the no-data floor: a player at 1 has no band to draw peers from, and
    ///         <see cref="Project" /> returns nothing for them on Phoenix 1.
    ///     </para>
    /// </summary>
    Task<double> CompetitiveLevel(MixEnum mix, ChartType chartType, Guid userId,
        CancellationToken cancellationToken);
}
