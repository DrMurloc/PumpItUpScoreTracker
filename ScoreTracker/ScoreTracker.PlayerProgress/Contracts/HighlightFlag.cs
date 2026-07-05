namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     Noteworthy-score flags, captured at write time so they stay historically true
///     (a read-time crown would drift as the top 50 moves under it).
/// </summary>
[Flags]
public enum HighlightFlag
{
    None = 0,

    /// <summary>The chart sat in the player's Pumbility top 50 when the score landed.</summary>
    PumbilityTop50 = 1,

    /// <summary>The score counts toward a not-yet-complete title.</summary>
    TitleProgress = 2,

    /// <summary>Score Quality at or above the 90th percentile vs comparable players (tie-inclusive).</summary>
    ScoreQuality90 = 4,

    /// <summary>A new pass in a (type, level) folder at ≥ 90% completion.</summary>
    FolderCompletion90 = 8,

    /// <summary>The batch raised Singles or Doubles competitive level and this score drove it.</summary>
    CompetitiveImprover = 16,

    /// <summary>One of the first 3 passes ever in this (type, level) folder.</summary>
    FolderDebut = 32
}
