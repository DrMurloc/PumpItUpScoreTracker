namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>
///     Board scores plus the instant they were true. <see cref="AsOf" /> is not decoration: these
///     sit beside live site scores on the same board, and a week-old number that does not say so
///     is a lie about who is ahead.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record OfficialTagScores(
    DateTimeOffset? AsOf,
    IReadOnlyList<OfficialTagScore> Scores);

[ExcludeFromCodeCoverage]
public sealed record OfficialTagScore(string Tag, Guid ChartId, int Place, int Score);
