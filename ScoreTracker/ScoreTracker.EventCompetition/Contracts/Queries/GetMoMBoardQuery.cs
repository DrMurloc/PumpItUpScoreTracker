namespace ScoreTracker.EventCompetition.Contracts.Queries;

/// <summary>
///     One board's ranked rows — published sessions in score order, earliest publication
///     winning a tie (§1). Drafts are never on a board.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMoMBoardQuery(Guid BoardId) : IQuery<MoMBoardView?>;
