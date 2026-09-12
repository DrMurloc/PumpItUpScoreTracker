namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     One row of the PIU Scores Hardmode leaderboard. <paramref name="Hardmode" /> IS the
///     PUMBILITY this page ranks on — the player's ordinary pool is not carried here, because a
///     board that prints both invites the reading that they are two comparable numbers when one
///     is a strict subset of the other (owner, 2026-09-12).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record HardmodeBoardRow(int Place, Guid UserId, double Hardmode, int Held);
