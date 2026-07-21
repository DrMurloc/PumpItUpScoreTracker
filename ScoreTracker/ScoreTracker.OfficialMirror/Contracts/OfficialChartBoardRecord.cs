namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>One chart's mirrored official board, in place order.</summary>
[ExcludeFromCodeCoverage]
public sealed record OfficialChartBoardRecord(DateTimeOffset AsOf,
    IReadOnlyList<OfficialChartBoardEntryRecord> Entries);

[ExcludeFromCodeCoverage]
public sealed record OfficialChartBoardEntryRecord(int Place, OfficialPlayerRecord Player, int Score);
