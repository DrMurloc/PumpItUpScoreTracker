namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>
///     One board's threshold picture. Entry is null until the board is full (the site caps
///     boards at 1000 — an unfull board has no cutline, any top 50 gets on). Tiers only
///     include rungs the board actually reaches.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record WhatItTakesRecord(
    DateTimeOffset? SnapshotAt,
    bool BoardFull,
    int BoardCount,
    CutlineTierRecord? Entry,
    IReadOnlyList<CutlineTierRecord> Tiers,
    IReadOnlyList<BoardCutlineRecord> Boards,
    IReadOnlyList<CutlineHistoryPointRecord> History);

/// <summary>A tier's value plus the four grade-level equivalents (null = no level clears it).</summary>
[ExcludeFromCodeCoverage]
public sealed record CutlineTierRecord(int Rank, decimal Value, decimal? WeekDelta, int? LevelForAAA,
    int? LevelForS, int? LevelForSS, int? LevelForSSS);

[ExcludeFromCodeCoverage]
public sealed record BoardCutlineRecord(string Type, decimal? EntryValue, decimal? WeekDelta, bool BoardFull);

[ExcludeFromCodeCoverage]
public sealed record CutlineHistoryPointRecord(DateTimeOffset At, decimal Value, int? LevelForAAA,
    int? LevelForS, int? LevelForSS, int? LevelForSSS);
