namespace ScoreTracker.EventCompetition.Contracts.Queries;

/// <summary>
///     Two sessions side by side (§11.3): on one board, or across seasons of one lineage —
///     same mix, same chart type — where the older session is also re-priced under the newer
///     season (D20). Null when either is missing or the two are a different sport (D15).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CompareMoMSessionsQuery(Guid SessionId, Guid OtherSessionId) : IQuery<MoMComparison?>;
