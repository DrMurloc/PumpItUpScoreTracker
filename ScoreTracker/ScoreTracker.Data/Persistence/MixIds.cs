using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Data.Persistence;

/// <summary>
///     The production Mix row IDs, previously copy-pasted as a private MixGuids dictionary
///     in four EF repositories. Persistence-level detail (the rows live in scores.Mix);
///     will need a rethink if non-prod environments ever get their own seed data.
/// </summary>
public static class MixIds
{
    public static readonly Guid XX = Guid.Parse("20F8CCF8-94B1-418D-B923-C375B042BDA8");
    public static readonly Guid Phoenix = Guid.Parse("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B");

    public static Guid For(MixEnum mix)
    {
        return mix switch
        {
            MixEnum.XX => XX,
            MixEnum.Phoenix => Phoenix,
            _ => throw new ArgumentOutOfRangeException(nameof(mix), mix, "No Mix row id known for mix")
        };
    }
}
