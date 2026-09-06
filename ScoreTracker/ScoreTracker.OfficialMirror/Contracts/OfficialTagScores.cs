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

/// <summary>
///     One board score, with the face the mirror swept beside it. The avatar rides here rather
///     than being looked up per row: these rows sit on a board next to site players who all have
///     one, and a row that is the only one without a picture reads as a placeholder rather than a
///     person. Null when the sweep never saw one.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record OfficialTagScore(string Tag, Guid ChartId, int Place, int Score, Uri? Avatar = null);
