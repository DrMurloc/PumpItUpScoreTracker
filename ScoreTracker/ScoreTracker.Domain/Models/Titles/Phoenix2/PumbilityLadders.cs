using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix2;

/// <summary>
///     The [S], [D] and [P.B] PUMBILITY ladders of a mix that plays by Phoenix 2's rules: every
///     rung's name, pool and threshold. Phoenix 2's are its own title list's. Rise keeps Phoenix 2's
///     rungs and raises the top of each ladder (docs/design/rise.md D27); Rise Arcade keeps Phoenix
///     2's numbers. Each mix holds its own title objects, linked and railed like Phoenix 2's, so a
///     rung's progress floor is its own ladder's.
/// </summary>
public static class PumbilityLadders
{
    /// <summary>Rise's rungs that sit above Phoenix 2's. Every other rung keeps Phoenix 2's number.</summary>
    private static readonly IReadOnlyDictionary<Name, int> RiseRaised = new Dictionary<Name, int>
    {
        [Name.From("[S] EXPERT LV.2")] = 17750,
        [Name.From("[S] EXPERT LV.3")] = 18000,
        [Name.From("[S] EXPERT LV.4")] = 18250,
        [Name.From("[S] EXPERT LV.5")] = 18500,
        [Name.From("[S] EXPERT LV.6")] = 18650,
        [Name.From("[S] EXPERT LV.7")] = 18750,
        [Name.From("[S] EXPERT LV.8")] = 18900,
        [Name.From("[S] EXPERT LV.9")] = 19000,
        [Name.From("[S] EXPERT LV.10")] = 19150,
        [Name.From("SINGLE MASTER")] = 19250,
        [Name.From("[D] EXPERT LV.2")] = 18250,
        [Name.From("[D] EXPERT LV.3")] = 18450,
        [Name.From("[D] EXPERT LV.4")] = 18750,
        [Name.From("[D] EXPERT LV.5")] = 19000,
        [Name.From("[D] EXPERT LV.6")] = 19150,
        [Name.From("[D] EXPERT LV.7")] = 19250,
        [Name.From("[D] EXPERT LV.8")] = 19400,
        [Name.From("[D] EXPERT LV.9")] = 19550,
        [Name.From("[D] EXPERT LV.10")] = 19650,
        [Name.From("DOUBLE MASTER")] = 19800,
        [Name.From("[P.B] ALEXANDRITE")] = 19200,
        [Name.From("ABYSS ABSOLUTE")] = 20100
    };

    private static readonly IReadOnlyList<Phoenix2PumbilityTitle> Phoenix2 =
        Phoenix2TitleList.BuildList().OfType<Phoenix2PumbilityTitle>().ToArray();

    private static readonly IReadOnlyList<Phoenix2PumbilityTitle> Rise = CopyOfPhoenix2(RiseRaised);

    private static readonly IReadOnlyList<Phoenix2PumbilityTitle> RiseArcade =
        CopyOfPhoenix2(new Dictionary<Name, int>());

    /// <summary>The mix's ladders in Phoenix 2's declaration order: [S], then [D], then [P.B].</summary>
    public static IReadOnlyList<Phoenix2PumbilityTitle> For(MixEnum mix)
    {
        return mix switch
        {
            MixEnum.Phoenix2 => Phoenix2,
            MixEnum.Rise => Rise,
            MixEnum.RiseArcade => RiseArcade,
            _ => throw new ArgumentOutOfRangeException(nameof(mix), mix,
                "Only a mix on Phoenix 2's PUMBILITY rules has these ladders")
        };
    }

    /// <summary>One pool's rungs, lowest threshold first.</summary>
    public static IReadOnlyList<Phoenix2PumbilityTitle> For(MixEnum mix, PumbilityPool pool)
    {
        return For(mix).Where(t => t.Pool == pool).OrderBy(t => t.CompletionRequired).ToArray();
    }

    private static IReadOnlyList<Phoenix2PumbilityTitle> CopyOfPhoenix2(IReadOnlyDictionary<Name, int> raised)
    {
        var titles = Phoenix2.Select(t => new Phoenix2PumbilityTitle(t.Name, t.Pool,
                raised.TryGetValue(t.Name, out var threshold) ? threshold : t.CompletionRequired, t.Tier))
            .ToArray();
        Phoenix2TitleList.LinkAndRail(titles);
        return titles;
    }
}
