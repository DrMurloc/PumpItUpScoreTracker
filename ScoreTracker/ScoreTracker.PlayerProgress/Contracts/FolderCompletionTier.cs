namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     The completion ladder a folder climbs. Crossing a tier is what fires a milestone, what
///     steps the chip's glow, and what the community bar measures against; 100 is the Folder
///     Lamp. Public because Web picks its treatment from the tier
///     (docs/design/folder-level-progression.md §2.2).
/// </summary>
public static class FolderCompletionTier
{
    /// <summary>Every chart in the folder passed.</summary>
    public const int Lamp = 100;

    /// <summary>The tiers, ascending.</summary>
    public static readonly IReadOnlyList<int> All = new[] { 20, 40, 60, 80, Lamp };

    /// <summary>
    ///     The highest tier a completion percent has reached, or 0 when it has not reached the
    ///     first one. A folder under 20% has no tier — its bar length is the whole story.
    /// </summary>
    public static int For(int completionPercent)
    {
        var reached = 0;
        foreach (var tier in All)
            if (completionPercent >= tier)
                reached = tier;
        return reached;
    }
}
