namespace ScoreTracker.EventCompetition.Contracts.Queries;

/// <summary>
///     The viewer's open draft on a board, if one exists. One draft at a time per board
///     (§10) — its only exits are Discard and Publish, so this is how Submit finds the
///     session a stray navigation abandoned.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMoMDraftQuery(Guid BoardId) : IQuery<MoMSessionView?>;
