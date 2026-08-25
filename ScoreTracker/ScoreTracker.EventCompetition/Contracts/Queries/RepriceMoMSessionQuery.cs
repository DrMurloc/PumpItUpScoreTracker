namespace ScoreTracker.EventCompetition.Contracts.Queries;

/// <summary>
///     Re-prices a session under another board's frozen configuration and splits what moved
///     (D20): the chart re-ratings and the scoring-table re-cut, each isolated, so a
///     cross-season comparison separates "I got better" from "the game changed". Null when
///     either side is missing or the boards are not the same (mix, chart type) — a different
///     chart type is a different sport and is never compared (D15).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RepriceMoMSessionQuery(Guid SessionId, Guid UnderBoardId)
    : IQuery<MoMSessionReprice?>;
