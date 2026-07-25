namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>
///     A linked account's official standing: the profile-link username, top-board count, chart
///     firsts, best single-chart placement, and where they sit on each PUMBILITY board
///     (Combined/Singles/Doubles + the computed CO-OP board). Null ranks mean not on that board.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record OfficialPlayerStandingRecord(
    string Username,
    int? PumbilityRank,
    int BoardsInTop,
    int NumberOnes,
    int? BestPlace,
    int? SinglesRank,
    int? DoublesRank,
    int? CoOpRank);
