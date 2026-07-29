namespace ScoreTracker.SharedKernel.Enums;

/// <summary>
///     Where a qualifier submission came from, which is what decides whether it owes a photo.
///     A score the player typed in needs one; a score read off the official site does not.
/// </summary>
public enum SubmissionSource
{
    /// <summary>Entered by the player, backed by a photo of the result screen.</summary>
    Manual = 0,

    /// <summary>Read from the official site by an import, so the site itself is the evidence.</summary>
    OfficialImport = 1
}
