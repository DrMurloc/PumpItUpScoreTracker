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

    /// <summary>A completed title's paragon level advanced. Title + Detail (new level).</summary>
    ParagonLevelGain,

    /// <summary>Every chart in a (type, level) folder passed. Detail = folder (e.g. "D23").</summary>
    FolderPassLamp,

    /// <summary>The folder's minimum letter grade reached a new floor. Detail = "D23|SS".</summary>
    FolderGradeLamp,

    /// <summary>The folder's minimum plate reached a new floor. Detail = "D23|UltimateGame".</summary>
    FolderPlateLamp,

    /// <summary>
    ///     Weekly-board placement changed. NewValue = the place, Title = the song,
    ///     Detail = the difficulty string (e.g. "D21"). SessionId stays null — weekly
    ///     registration follows its own eligibility flow, not the score batches.
    /// </summary>
    WeeklyPlacement
}
