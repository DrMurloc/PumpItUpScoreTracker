using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Services.Contracts;

/// <summary>
///     One chart to project, with the level its peers are read at. The level travels with the
///     id because the projector never reads the catalog — it asks the score store for a level
///     band rather than a list of several hundred chart ids.
/// </summary>
public readonly record struct ProjectionTarget(Guid ChartId, int Level);

/// <summary>What a projection run is asked for.</summary>
/// <param name="CompetitiveWindow">
///     Competitive-level half-width of the peer gate. Deliberately required rather than
///     defaulted: PUMBILITY wants ±1.0 because it quotes the projected number and that window
///     is measured optimal for accuracy, while a tier list wants ±0.5 because it only ranks
///     charts against each other and the rest of the site calls a competitive peer ±0.5. A
///     default here would let one of them drift onto the other's answer silently.
/// </param>
public sealed record ScoreProjectionRequest(
    MixEnum Mix,
    ChartType ChartType,
    Guid UserId,
    IReadOnlyCollection<ProjectionTarget> Targets,
    double CompetitiveWindow);

/// <summary>
///     What a projection run produced, and the cohort it produced it from.
/// </summary>
/// <param name="Scores">
///     The projected score per chart. A chart no peer has played is simply absent — absent means
///     "no opinion", never zero, and callers must render it as such.
/// </param>
/// <param name="PeerCount">
///     How many distinct players' scores are actually behind <paramref name="Scores" /> — not how
///     many sit in the competitive band, most of whom have played none of these charts. A surface
///     quoting a cohort size has to quote the one that voted, or the figure overstates the
///     evidence by roughly the share of the band that skipped the folder.
/// </param>
/// <param name="CompetitiveLevel">
///     The level the peers were matched around, so a caller can state the band it read without
///     resolving the player's level a second way.
/// </param>
/// <param name="MeanFreshness">
///     Mean growth weight across the scores that contributed, on 0..1: 1.0 means every peer has
///     the level they had when they set their score, and lower means the cohort has outgrown its
///     own evidence. Zero when nothing contributed.
/// </param>
public sealed record ScoreProjection(
    IReadOnlyDictionary<Guid, PhoenixScore> Scores,
    int PeerCount,
    double CompetitiveLevel,
    double MeanFreshness)
{
    /// <summary>No opinion, for the runs that stop before a cohort exists.</summary>
    public static ScoreProjection None(double competitiveLevel = 0)
    {
        return new ScoreProjection(new Dictionary<Guid, PhoenixScore>(), 0, competitiveLevel, 0);
    }
}

/// <summary>
///     "What would this player score on this chart" — the one implementation of that question
///     (docs/design/pumbility-overhaul.md §4.1), shared so the PUMBILITY projection and the
///     personalized Score tier list cannot answer it differently.
///     <para>
///         It answers only that. Which charts are worth asking about is the caller's business:
///         PUMBILITY asks about unplayed charts that could clear its pool bar, the tier list
///         asks about every chart in the folder being viewed.
///     </para>
/// </summary>
public interface IScoreProjector
{
    /// <summary>
    ///     The projected scores, and the cohort behind them. The cohort figures ride along because
    ///     the run already computes them and a second pass to recover them would re-read every
    ///     peer's scores — a surface that wants to say what its number rests on should not have to
    ///     pay for the sweep twice.
    /// </summary>
    Task<ScoreProjection> Project(ScoreProjectionRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    ///     The level this projection matches peers on. Published because a caller narrowing the
    ///     charts it asks about has to narrow them around the same number the peers are drawn
    ///     around — two readings of "what level is this player" would let the scope and the
    ///     cohort disagree at a mix launch, which is the one time the fallback inside fires.
    ///     <para>
    ///         1 is the no-data floor: a player at 1 has no band to draw peers from, and
    ///         <see cref="Project" /> returns nothing for them.
    ///     </para>
    /// </summary>
    Task<double> CompetitiveLevel(MixEnum mix, ChartType chartType, Guid userId,
        CancellationToken cancellationToken);
}
