namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     One player's Hardmode standing on the combined board, as their own board shows it
///     (docs/design/hardmode-leaderboard.md D31): the number, how much of a fifty it holds, the place
///     — null while the player has no Hardmode number — the field their board counts (public accounts
///     plus their own, D17), and how many charts qualify this week.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record HardmodeStandingRecord(double Total, int Held, int? Place, int Field, int QualifyingCharts);
