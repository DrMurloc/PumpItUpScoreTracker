namespace ScoreTracker.EventCompetition.Contracts.Queries;

/// <summary>
///     Where a board lives: the season and chart type behind a board id. A board's id is its
///     legacy tournament id, so this is what lets every old /Tournament/Stamina/{id} link 301
///     to the season with that board selected. Null for an unknown id.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMoMBoardLocatorQuery(Guid BoardId) : IQuery<MoMBoardLocator?>;
