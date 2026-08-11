using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Whether an import may record a run that failed the stage as the player's best.
///     <para>
///         Three surfaces ask this question — the import page, the Import Scores widget and its
///         configurator — and each used to answer it with its own copy of the rule. They are here
///         so they cannot drift.
///     </para>
/// </summary>
public static class BrokenScorePreference
{
    /// <summary>
    ///     What the control reads when the player has never chosen, which mirrors the official
    ///     site rather than expressing a preference: Phoenix 2 keeps a personal best for a failed
    ///     stage and its best-scores list carries them, Phoenix keeps none and its list does not.
    /// </summary>
    public static bool DefaultFor(MixEnum mix)
    {
        return mix == MixEnum.Phoenix2;
    }
}
