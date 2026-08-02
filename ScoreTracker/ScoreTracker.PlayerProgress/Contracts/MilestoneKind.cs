namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     Session-level milestone kinds. Stored by name (not value) so reordering the enum
///     can never corrupt captured history.
/// </summary>
public enum MilestoneKind
{
    /// <summary>Pumbility (top-50 rating sum) went up. OldValue → NewValue.</summary>
    PumbilityGain,

    /// <summary>Singles competitive level went up. OldValue → NewValue. Combined competitive is deliberately never a milestone.</summary>
    SinglesCompetitiveGain,

    /// <summary>Doubles competitive level went up. OldValue → NewValue.</summary>
    DoublesCompetitiveGain,

    /// <summary>A title completed. Title carries the name.</summary>
    TitleCompleted,

    /// <summary>Every chart in a (type, level) folder passed. Detail = folder (e.g. "D23").</summary>
    FolderPassLamp,

    /// <summary>
    ///     A folder's completion crossed a tier, or its grade improved, or both. Detail is a
    ///     <see cref="FolderProgressDetail" /> — one kind carries every folder movement rather
    ///     than a kind per shape (docs/design/folder-level-progression.md §5.1).
    /// </summary>
    FolderProgress,

    /// <summary>The folder's minimum letter grade reached a new floor. Detail = "D23|SS".</summary>
    FolderGradeLamp,

    /// <summary>The folder's minimum plate reached a new floor. Detail = "D23|UltimateGame".</summary>
    FolderPlateLamp,

    /// <summary>
    ///     Weekly-board placement changed. NewValue = the place, Title = the song,
    ///     Detail = the difficulty string (e.g. "D21"). SessionId stays null — weekly
    ///     registration follows its own eligibility flow, not the score batches.
    /// </summary>
    WeeklyPlacement,

    /// <summary>
    ///     Singles PUMBILITY pool went up. OldValue → NewValue. Phoenix 2 only — its
    ///     title ladder gates on the per-type pools, so the pools are milestones there.
    /// </summary>
    SinglesPumbilityGain,

    /// <summary>Doubles PUMBILITY pool went up. OldValue → NewValue. Phoenix 2 only.</summary>
    DoublesPumbilityGain,

    /// <summary>
    ///     A not-yet-complete title moved. OldValue → NewValue are percents, Title is the
    ///     title, Detail is "S21|3120|4000" (folder | current | required). Several fire per
    ///     batch by design — the Sessions page renders these as progress bars, NOT as
    ///     milestone strips, which is what the earlier "no deltas as milestones" call was
    ///     protecting against (docs/design/session-breakdown.md §2.2).
    /// </summary>
    TitleProgress,

    /// <summary>
    ///     The player's estimated place on the official PUMBILITY board improved.
    ///     OldValue → NewValue, Detail is the board name. Estimated by ranking our own pool
    ///     against the last sealed board, so it moves per import rather than per sweep.
    ///     Mints on improvement only — an undo recomputes stats downward and must not
    ///     announce the rank it just cost.
    /// </summary>
    OfficialPumbilityRank
}
