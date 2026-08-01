namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>
///     What a scoped delete removes. Derived state is deliberately absent: Pumbility, titles,
///     folder lamps and player stats are recomputed from scores, so they cannot outlive their
///     inputs and are never offered as a choice (docs/design/delete-my-data.md D9).
/// </summary>
[Flags]
public enum ScoreDeletionItems
{
    None = 0,

    /// <summary>The records themselves — Phoenix bests, the XX legacy table, and per-score stats.</summary>
    BestScores = 1,

    /// <summary>The journal: every play observed, best or not.</summary>
    PlayHistory = 2,

    /// <summary>Rating-over-time history.</summary>
    RatingHistory = 4,

    /// <summary>Session roundups and their highlights.</summary>
    Highlights = 8,

    /// <summary>Title and folder-lamp moments. Not recomputed, so they would otherwise strand.</summary>
    Milestones = 16,

    Everything = BestScores | PlayHistory | RatingHistory | Highlights | Milestones
}
