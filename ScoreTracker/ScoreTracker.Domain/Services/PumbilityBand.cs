using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Services;

/// <summary>
///     One band of a Phoenix 2 PUMBILITY ladder: the players the Breakdown card compares you
///     against (docs/design/pumbility-overhaul.md D68). A band is a half-open range on a pool sum —
///     the merged ladder's gems and the five levels inside each, and the [S] / [D] ladders' rungs,
///     which have nothing coarser inside them to fall back to.
///     <para>
///         Phoenix 1 has no band table: its cohort is the difficulty title already stored per
///         player, which is rating earned on one level rather than a range on a pool.
///     </para>
/// </summary>
/// <param name="Gem">The gem a merged level sits in, and null on every other band.</param>
/// <param name="Level">
///     Which of the gem's five levels this is, for a surface that lists them under the gem's own
///     name and would otherwise repeat it on every line. Null on a gem, on a typed rung, and on the
///     capstone, which is a gem with no levels inside it.
/// </param>
public sealed record PumbilityBand(PumbilityPool Pool, Name Name, double Floor, double? Ceiling, Name? Gem = null,
    int? Level = null)
{
    /// <summary>
    ///     How many players a level needs before it is read in place of the gem around it. Measured
    ///     (§4.14): 20 and 25 leave the same 125 of 297 accounts on their own level while 30 costs 27
    ///     of them, so 25 is the top of that plateau — and whatever the number, flipping off lands on
    ///     a gem, the smallest of which holds 23.
    /// </summary>
    public const int MinimumForLevel = 25;

    private static readonly IReadOnlyList<PumbilityBand> MergedLevels = BuildMergedLevels();
    private static readonly IReadOnlyList<PumbilityBand> MergedGems = BuildMergedGems();
    private static readonly IReadOnlyList<PumbilityBand> SinglesRungs = BuildTyped(PumbilityPool.Singles);
    private static readonly IReadOnlyList<PumbilityBand> DoublesRungs = BuildTyped(PumbilityPool.Doubles);

    /// <summary>Whether a pool sum stands on this band. Never rounds: a pool is fifty fractions.</summary>
    public bool Holds(double pool)
    {
        return pool >= Floor && (Ceiling is not { } top || pool < top);
    }

    /// <summary>Every band of a ladder, weakest first — what the selector lists.</summary>
    public static IReadOnlyList<PumbilityBand> Levels(PumbilityPool pool)
    {
        return pool switch
        {
            PumbilityPool.Singles => SinglesRungs,
            PumbilityPool.Doubles => DoublesRungs,
            _ => MergedLevels
        };
    }

    /// <summary>The merged ladder's gems. The typed ladders have none.</summary>
    public static IReadOnlyList<PumbilityBand> Gems()
    {
        return MergedGems;
    }

    /// <summary>The band a pool sum stands on, or null when it sits under the ladder's first rung.</summary>
    public static PumbilityBand? LevelOf(PumbilityPool pool, double value)
    {
        return Levels(pool).LastOrDefault(b => b.Holds(value));
    }

    /// <summary>The gem a merged pool sum stands in, or null under the ladder.</summary>
    public static PumbilityBand? GemOf(double value)
    {
        return MergedGems.LastOrDefault(b => b.Holds(value));
    }

    /// <summary>A band by the name a saved choice carries, over both the levels and the gems.</summary>
    public static PumbilityBand? ByName(PumbilityPool pool, Name name)
    {
        return Levels(pool).FirstOrDefault(b => b.Name == name)
               ?? (pool == PumbilityPool.Total ? MergedGems.FirstOrDefault(b => b.Name == name) : null);
    }

    private static IReadOnlyList<PumbilityBand> BuildMergedLevels()
    {
        return Phoenix2PumbilityLevel.All
            .Where(rung => rung.IsRanked)
            .Select(rung => new PumbilityBand(PumbilityPool.Total, NameOf(rung), rung.Threshold,
                rung.NextThreshold, rung.Gem, rung.Level))
            .ToArray();
    }

    private static Name NameOf(Phoenix2PumbilityLevel rung)
    {
        return rung.Level is { } level && rung.Gem is { } gem ? Name.From($"{gem} LV.{level}") : rung.Gem!.Value;
    }

    private static IReadOnlyList<PumbilityBand> BuildMergedGems()
    {
        var gems = Phoenix2PumbilityLevel.All.Where(rung => rung.IsRanked && rung.Gem is { })
            .GroupBy(rung => rung.Gem!.Value)
            .Select(g => (Gem: g.Key, Floor: g.Min(rung => rung.Threshold)))
            .OrderBy(g => g.Floor)
            .ToArray();
        return gems.Select((g, i) => new PumbilityBand(PumbilityPool.Total, g.Gem, g.Floor,
            i + 1 < gems.Length ? gems[i + 1].Floor : null)).ToArray();
    }

    private static IReadOnlyList<PumbilityBand> BuildTyped(PumbilityPool pool)
    {
        var rungs = Phoenix2TitleList.BuildList().OfType<Phoenix2PumbilityTitle>()
            .Where(title => title.Pool == pool)
            .OrderBy(title => title.CompletionRequired)
            .ToArray();
        return rungs.Select((title, i) => new PumbilityBand(pool, title.Name, title.CompletionRequired,
            i + 1 < rungs.Length ? rungs[i + 1].CompletionRequired : null)).ToArray();
    }
}
