namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>
///     One row of the Official Boards Hardmode leaderboard. <paramref name="Hardmode" /> IS the
///     PUMBILITY this page ranks on; the player's published pool is deliberately not carried
///     (owner, 2026-09-12), which also spares the board a rating-board read per load.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record OfficialHardmodeRow(int Place, OfficialPlayerRecord Player, double Hardmode,
    int Held);
