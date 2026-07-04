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

    // Matched pair with the production seed script ("PIU Phoenix 2 - ChartMix seed.sql") —
    // the scores.Mix row must be inserted with exactly this id.
    public static readonly Guid Phoenix2 = Guid.Parse("A9B7D3C1-52E8-4F06-9B1A-2F8C33E01948");

    public static Guid For(MixEnum mix)
    {
        return mix switch
        {
            MixEnum.XX => XX,
            MixEnum.Phoenix => Phoenix,
            MixEnum.Phoenix2 => Phoenix2,
            _ => throw new ArgumentOutOfRangeException(nameof(mix), mix, "No Mix row id known for mix")
        };
    }
}
