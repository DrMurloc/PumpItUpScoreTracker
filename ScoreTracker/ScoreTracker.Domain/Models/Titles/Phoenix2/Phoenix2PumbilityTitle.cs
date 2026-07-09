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
    public Phoenix2PumbilityTitle(Name name, PumbilityPool pool, int threshold)
        : base(name, $"{Label(pool)} of {threshold:N0}+", "Difficulty", threshold)
    {
        Pool = pool;
    }

    public PumbilityPool Pool { get; }

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
