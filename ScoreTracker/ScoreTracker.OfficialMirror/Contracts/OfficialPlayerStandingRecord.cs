namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>Username (the official-profile link target), PUMBILITY rank, and top-board count.</summary>
[ExcludeFromCodeCoverage]
public sealed record OfficialPlayerStandingRecord(string Username, int? PumbilityRank, int BoardsInTop);
