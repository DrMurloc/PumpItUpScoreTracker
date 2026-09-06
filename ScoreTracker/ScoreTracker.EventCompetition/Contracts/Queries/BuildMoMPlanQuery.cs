using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.EventCompetition.Contracts.Queries;

/// <summary>
///     Your record book priced for one board, and the set the solver suggests inside it
///     (docs/design/march-of-murlocs.md §11.5). Null when the board does not exist or you are not
///     signed in — a plan is built from your own scores.
///     <para>
///         Rest per chart is the one control that matters and the page re-asks on every change, so
///         this is a read the Planner makes often. Everything it needs is a cached read but the
///         projector, which only runs at the two peer rungs — and only when
///         <see cref="IncludeProjected" /> asks for charts you have never passed. Off by default: a
///         plan you can take to a machine is built from what you have actually scored.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BuildMoMPlanQuery(
    Guid BoardId,
    MoMEnergy Energy = MoMEnergy.Good,
    MoMPush Push = MoMPush.Steady,
    int RestSeconds = 35,
    bool IncludeProjected = false) : IQuery<MoMPlanView?>;
