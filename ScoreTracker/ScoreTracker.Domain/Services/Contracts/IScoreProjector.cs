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
    ///     The projected score per chart. A chart no peer has played is simply absent — absent
    ///     means "no opinion", never zero, and callers must render it as such.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, PhoenixScore>> Project(ScoreProjectionRequest request,
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
