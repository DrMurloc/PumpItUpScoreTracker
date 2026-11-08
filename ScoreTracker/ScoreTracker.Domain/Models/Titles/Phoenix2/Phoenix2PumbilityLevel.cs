using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix2;

/// <summary>
///     One rung of the Phoenix 2 PUMBILITY level ladder: the sub-step inside a [P.B] gem that the
///     game awards but states nowhere in text. Its only carrier on piugame is the badge image beside
///     a player's number, <c>/l_img/pumbility/pumbility_NN.png</c>, whose file index is this
///     <see cref="Index" /> and whose art draws <see cref="Level" /> as a numeral
///     (docs/design/pumbility-levels.md).
///     <para>
///         Thirty-seven rungs: index 0 for a pool that has not reached BRONZE, five levels inside
///         each of the seven gems, then the capstone. A gem's five levels split its span evenly —
///         200 across a 1,000-wide gem, 500 across the 2,500-wide BRONZE and SILVER — so the rungs
///         are derived from the mix's [P.B] titles rather than listed, and a gem cannot gain a rung
///         without gaining a threshold.
///     </para>
///     <para>
///         Per mix, for the mixes on Phoenix 2's PUMBILITY rules: the gems are the mix's own [P.B]
///         rungs (<see cref="PumbilityLadders" />), so Rise's raised ALEXANDRITE and ABYSS ABSOLUTE
///         move its levels with them. Asking for a mix without the ladder throws, so a caller cannot
///         skip the gate on the mix.
///     </para>
///     <para>
///         Evidence, and where it runs out: indices 21–36 are observed — 1,000 rows of the live
///         PUMBILITY board agreed with this table, and each cutoff fell in an interval containing
///         exactly one round number. Below DIAMOND the board has no wearers, so those thresholds are
///         the even split rather than an observation. Every rung's art exists and numbers itself, so
///         the shape is certain even where the numbers are inferred. Re-check with
///         <c>Phoenix2PumbilityLevelBadgeReconTests</c> in ScoreTracker.ExplorationTests.
///     </para>
/// </summary>
public readonly record struct Phoenix2PumbilityLevel
{
    /// <summary>Levels inside a gem. The capstone is the one rung that has none.</summary>
    public const int LevelsPerGem = 5;

    /// <summary>The badge index of the capstone, and so the top of the ladder.</summary>
    public const int CapstoneIndex = 36;

    private static readonly IReadOnlyDictionary<MixEnum, Phoenix2PumbilityLevel[]> RungsByMix =
        Enum.GetValues<MixEnum>().Where(m => m.UsesPhoenix2Pumbility()).ToDictionary(m => m, BuildRungs);

    private Phoenix2PumbilityLevel(int index, Name? gem, int? level, int threshold, int? nextThreshold)
    {
        Index = index;
        Gem = gem;
        Level = level;
        Threshold = threshold;
        NextThreshold = nextThreshold;
    }

    /// <summary>The badge index, 0..36 — the site's own numbering, and the ladder's total order.</summary>
    public int Index { get; }

    /// <summary>The [P.B] title this rung sits inside. Null at index 0, which is inside no gem.</summary>
    public Name? Gem { get; }

    /// <summary>1..5 within the gem. Null at index 0 and at the capstone, neither of which has levels.</summary>
    public int? Level { get; }

    /// <summary>
    ///     The pool this rung starts at — an authored round number, not a measurement, which is why
    ///     it is an int and why presentation renders it N0 (see the PUMBILITY precision rule).
    /// </summary>
    public int Threshold { get; }

    /// <summary>Where the next rung starts, or null at the capstone, which has nothing above it.</summary>
    public int? NextThreshold { get; }

    /// <summary>False only at index 0: a pool that has not reached BRONZE holds no gem.</summary>
    public bool IsRanked => Index > 0;

    public bool IsCapstone => Index == CapstoneIndex;

    /// <summary>Every rung of the mix's ladder, weakest first. Index 0 is the first entry, the capstone the last.</summary>
    public static IReadOnlyList<Phoenix2PumbilityLevel> All(MixEnum mix) => RungsOf(mix);

    /// <summary>
    ///     The rung a total PUMBILITY pool stands on. Takes the raw pool and never rounds it: a pool
    ///     is fifty fractional contributions, and rounding one before the compare promotes a player
    ///     to a rung they have not reached.
    /// </summary>
    public static Phoenix2PumbilityLevel From(MixEnum mix, double totalPumbility)
    {
        var rungs = RungsOf(mix);
        for (var i = rungs.Length - 1; i >= 0; i--)
            if (totalPumbility >= rungs[i].Threshold)
                return rungs[i];
        return rungs[0];
    }

    /// <summary>The rung at a badge index, or null when the index is off the ladder.</summary>
    public static Phoenix2PumbilityLevel? FromIndex(MixEnum mix, int index)
    {
        var rungs = RungsOf(mix);
        return index >= 0 && index < rungs.Length ? rungs[index] : null;
    }

    /// <summary>A gem's five levels, weakest first — the capstone's single rung for the capstone.</summary>
    public static IReadOnlyList<Phoenix2PumbilityLevel> LevelsOf(MixEnum mix, Name gem) =>
        RungsOf(mix).Where(r => r.Gem is { } g && g == gem).ToArray();

    /// <summary>
    ///     What this pool still owes the next rung, or null at the capstone. Never negative — a pool
    ///     at or past its own threshold owes the rung above it, not this one.
    /// </summary>
    public double? ToNext(double totalPumbility) =>
        NextThreshold is { } next ? Math.Max(0, next - totalPumbility) : null;

    private static Phoenix2PumbilityLevel[] RungsOf(MixEnum mix)
    {
        return RungsByMix.TryGetValue(mix, out var rungs)
            ? rungs
            : throw new ArgumentOutOfRangeException(nameof(mix), mix, "This mix has no PUMBILITY level ladder");
    }

    /// <summary>
    ///     Each gem of the mix, weakest first, with the badge index its first level takes and the pool
    ///     it starts at: the thresholds on the mix's own [P.B] titles.
    /// </summary>
    private static Phoenix2PumbilityLevel[] BuildRungs(MixEnum mix)
    {
        var gems = PumbilityLadders.For(mix, PumbilityPool.Total)
            .Select((title, i) => (Gem: title.Name, FirstIndex: 1 + i * LevelsPerGem, Threshold: title.CompletionRequired))
            .ToArray();
        if (gems[^1].FirstIndex != CapstoneIndex)
            throw new InvalidOperationException($"{mix}'s gem ladder does not end on the capstone badge");

        var rungs = new List<Phoenix2PumbilityLevel>
        {
            // Index 0 is a rung, not an absence: the site draws a blank grey sphere for it.
            new(0, null, null, 0, gems[0].Threshold)
        };

        for (var g = 0; g < gems.Length; g++)
        {
            var (gem, firstIndex, threshold) = gems[g];
            var isCapstone = g == gems.Length - 1;
            if (isCapstone)
            {
                rungs.Add(new Phoenix2PumbilityLevel(firstIndex, gem, null, threshold, null));
                continue;
            }

            // The gem's span split five ways. Integer arithmetic throughout: every span is a
            // multiple of five (ratcheted), so no rung threshold is ever a fraction.
            var span = gems[g + 1].Threshold - threshold;
            for (var level = 1; level <= LevelsPerGem; level++)
            {
                var start = threshold + span * (level - 1) / LevelsPerGem;
                var next = threshold + span * level / LevelsPerGem;
                rungs.Add(new Phoenix2PumbilityLevel(firstIndex + level - 1, gem, level, start, next));
            }
        }

        return rungs.ToArray();
    }
}
