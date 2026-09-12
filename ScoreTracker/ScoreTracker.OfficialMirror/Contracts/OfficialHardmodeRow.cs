namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>
///     One row of the Official Boards Hardmode leaderboard. <paramref name="Pumbility" /> is the
///     pool piugame itself publishes for that player where the mirror holds it, so the row can
///     make the same comparison the PIU Scores board makes; null where no rating board carried
///     them.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record OfficialHardmodeRow(int Place, int OfficialPlayerId, string Username, double Hardmode,
    int Held, double? Pumbility);
