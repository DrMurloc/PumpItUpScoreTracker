using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.SharedKernel.Models;

/// <summary>
///     Why a stage broke, as far as the judgement counts can say.
///     <see cref="IsNonLifebarBreak" /> is the only claim ever made with certainty: the life bar
///     provably could not have emptied, so one of Phoenix 2's Stage Pass commands ended the run.
///     The two targets are independent — either, both or neither may be named, and naming neither
///     does not weaken the flag (docs/design/pass-command-detection.md D31).
/// </summary>
[ExcludeFromCodeCoverage]
public readonly record struct StageBreakCause(
    bool IsNonLifebarBreak,
    PhoenixPlate? PassPlate,
    PhoenixLetterGrade? PassGrade)
{
    /// <summary>The life bar could have emptied, or there was not enough to tell. No claim.</summary>
    public static readonly StageBreakCause Unattributed = new(false, null, null);

    /// <summary>A command ended the run and we could name its target.</summary>
    public bool IsNamed => PassPlate != null || PassGrade != null;
}
