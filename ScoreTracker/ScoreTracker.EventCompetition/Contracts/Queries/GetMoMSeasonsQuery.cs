namespace ScoreTracker.EventCompetition.Contracts.Queries;

/// <summary>
///     Every season newest first with per-board session counts, winners and the viewer's own
///     standing — the Past Seasons dialog's one read (§11.8).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMoMSeasonsQuery : IQuery<IReadOnlyList<MoMSeasonListing>>;
