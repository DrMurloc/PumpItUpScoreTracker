namespace ScoreTracker.EventCompetition.Contracts.Queries;

/// <summary>
///     One session in full (§11.3): its four numbers with their places on the board, its
///     charts on the clock, every other session on the board (for the marks and the compare
///     picker) and the owner's sessions in other seasons of the same board lineage. Null when
///     the session does not exist, or is a draft and the viewer is not its owner.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMoMSessionQuery(Guid SessionId, Guid? ViewerId = null) : IQuery<MoMSessionView?>;
