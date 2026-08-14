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
///         are derived from <see cref="Gems" /> rather than listed, and a gem cannot gain a rung
///         without gaining a threshold.
///     </para>
///     <para>
///         Phoenix 2 only. Phoenix has no gem ladder, so callers gate on the mix before asking;
///         there is deliberately no <c>MixEnum</c> parameter to make that gate look optional.
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

    /// <summary>
    ///     Each gem, the badge index its first level takes, and the pool it starts at. The
    ///     thresholds are the ones on the [P.B] titles themselves — <c>Phoenix2PumbilityLevelTests</c>
    ///     asserts the two agree, because a gem that moved here and not there would put every player
    ///     on the wrong rung silently.
    /// </summary>
    private static readonly (string Gem, int FirstIndex, int Threshold)[] Gems =
    {
        ("[P.B] BRONZE", 1, 10000),
        ("[P.B] SILVER", 6, 12500),
        ("[P.B] GOLD", 11, 15000),
        ("[P.B] PLATINUM", 16, 16000),
        ("[P.B] DIAMOND", 21, 17000),
        ("[P.B] RED BERYL", 26, 18000),
        ("[P.B] ALEXANDRITE", 31, 19000),
        ("ABYSS ABSOLUTE", CapstoneIndex, 20000)
    };

    private static readonly Phoenix2PumbilityLevel[] Rungs = BuildRungs();

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

    /// <summary>Every rung, weakest first. Index 0 is the first entry, the capstone the last.</summary>
    public static IReadOnlyList<Phoenix2PumbilityLevel> All => Rungs;

    /// <summary>
    ///     The rung a total PUMBILITY pool stands on. Takes the raw pool and never rounds it: a pool
    ///     is fifty fractional contributions, and rounding one before the compare promotes a player
    ///     to a rung they have not reached.
    /// </summary>
    public static Phoenix2PumbilityLevel From(double totalPumbility)
    {
        for (var i = Rungs.Length - 1; i >= 0; i--)
            if (totalPumbility >= Rungs[i].Threshold)
                return Rungs[i];
        return Rungs[0];
    }

    /// <summary>The rung at a badge index, or null when the index is off the ladder.</summary>
    public static Phoenix2PumbilityLevel? FromIndex(int index) =>
        index >= 0 && index < Rungs.Length ? Rungs[index] : null;

    /// <summary>A gem's five levels, weakest first — the capstone's single rung for the capstone.</summary>
    public static IReadOnlyList<Phoenix2PumbilityLevel> LevelsOf(Name gem) =>
        Rungs.Where(r => r.Gem is { } g && g == gem).ToArray();

    /// <summary>
    ///     What this pool still owes the next rung, or null at the capstone. Never negative — a pool
    ///     at or past its own threshold owes the rung above it, not this one.
    /// </summary>
    public double? ToNext(double totalPumbility) =>
        NextThreshold is { } next ? Math.Max(0, next - totalPumbility) : null;

    private static Phoenix2PumbilityLevel[] BuildRungs()
    {
        var rungs = new List<Phoenix2PumbilityLevel>
        {
            // Index 0 is a rung, not an absence: the site draws a blank grey sphere for it.
            new(0, null, null, 0, Gems[0].Threshold)
        };

        for (var g = 0; g < Gems.Length; g++)
        {
            var (gem, firstIndex, threshold) = Gems[g];
            var isCapstone = g == Gems.Length - 1;
            if (isCapstone)
            {
                rungs.Add(new Phoenix2PumbilityLevel(firstIndex, gem, null, threshold, null));
                continue;
            }

            // The gem's span split five ways. Integer arithmetic throughout: every span is a
            // multiple of five (ratcheted), so no rung threshold is ever a fraction.
            var span = Gems[g + 1].Threshold - threshold;
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
