namespace ScoreTracker.EventCompetition.Contracts.Queries;

/// <summary>
///     One session with its chart rows. A draft is visible only to its owner (D17) — for
///     anyone else the query answers null, indistinguishable from a session that does not
///     exist.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMoMSessionQuery(Guid SessionId) : IQuery<MoMSessionView?>;
