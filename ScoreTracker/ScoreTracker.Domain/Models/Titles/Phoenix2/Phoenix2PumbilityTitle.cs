using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix2;

/// <summary>Which Phoenix 2 PUMBILITY pool a title gates on.</summary>
public enum PumbilityPool
{
    Total,
    Singles,
    Doubles
}

/// <summary>
///     A Phoenix 2 title earned by reaching a PUMBILITY threshold — the [S]/[D] ladders and
///     the hidden total-pumbility tiers. Progress IS the pool value, computed once per build
///     by <see cref="Phoenix2TitleList.BuildProgress" /> (a top-50 pool can't be accumulated
///     attempt-by-attempt), never through per-attempt application.
/// </summary>
public sealed class Phoenix2PumbilityTitle : PhoenixTitle
{
    /// <param name="tier">
    ///     The band of ten this rung sits in — Intermediate, Advanced, Expert — or null for a
    ///     ladder with no bands (the merged [P.B] gems). The page draws a band per line, so
    ///     thirty-one rungs read as three tens and a capstone rather than one long row.
    /// </param>
    public Phoenix2PumbilityTitle(Name name, PumbilityPool pool, int threshold, Name? tier = null)
        : base(name, $"{Label(pool)} of {threshold:N0}+", "Difficulty", threshold)
    {
        Pool = pool;
        Tier = tier;
    }

    public PumbilityPool Pool { get; }

    /// <summary>The band of ten this rung sits in, and the rail it draws on. See the constructor.</summary>
    public Name? Tier { get; }

    public override bool PopulatesFromDatabase => false;

    private static string Label(PumbilityPool pool)
    {
        return pool switch
        {
            PumbilityPool.Singles => "Single PUMBILITY",
            PumbilityPool.Doubles => "Double PUMBILITY",
            _ => "Total PUMBILITY"
        };
    }
}
