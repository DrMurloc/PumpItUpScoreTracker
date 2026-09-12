namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     One row of the PIU Scores Hardmode leaderboard. <paramref name="Pumbility" /> is the
///     player's real pool for the same type, which is the comparison that makes the board
///     interesting — the same account's two numbers side by side.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record HardmodeBoardRow(int Place, Guid UserId, double Hardmode, int Held, double Pumbility);
