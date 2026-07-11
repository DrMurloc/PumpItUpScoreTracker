namespace ScoreTracker.SharedKernel.Enums;

/// <summary>
///     Per-mix capability flags for the tier-list surfaces (tier-lists overhaul design
///     doc §8). Skill tags are Phoenix-1-only until piucenter data covers later mixes —
///     the flag flips per mix when their export does, no code change downstream.
/// </summary>
public static class MixCapabilities
{
    public static bool HasSkillData(this MixEnum mix)
    {
        return mix == MixEnum.Phoenix;
    }
}
