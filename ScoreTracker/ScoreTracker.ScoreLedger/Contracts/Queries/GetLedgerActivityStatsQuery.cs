namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     Site-wide ledger volume for the public front door (docs/design/front-door.md):
///     total best-attempt counts across both score models, plus the scores-recorded
///     pulse for the trailing 30 days. Anonymous hot path — results are served from an
///     in-process cache, so the query is safe to dispatch on every landing-page render.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetLedgerActivityStatsQuery : IQuery<LedgerActivityStats>;
