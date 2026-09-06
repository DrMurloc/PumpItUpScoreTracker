using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     Whether the mirror can see the whole of a board player's PUMBILITY pool
///     (docs/design/pumbility-overhaul.md D60).
///     <para>
///         piugame prints each player's per-type pool on its own board, so a fifty rebuilt from the
///         chart rows the mirror holds can be checked against it. The gap runs one way only — a
///         chart we cannot see is a chart missing from our copy, never an extra one — which is what
///         makes the shortfall a measure of completeness rather than a guess.
///     </para>
/// </summary>
internal static class BoardPoolCheck
{
    /// <summary>
    ///     How far a rebuilt pool may fall short and still be believed: <b>270</b>, the plate bonus
    ///     of an entire fifty of Perfect-Gamed 25s. Phoenix 2 prices a chart
    ///     Base(level) × (grade + plateBonus); Base(25) is 260 for a Double and 270 for a Single,
    ///     which prices one level up, and a Perfect Game's plate bonus is 0.020 — so 5.20 a chart
    ///     and 260 or 270 across fifty. A board row carries a score and <b>no plate</b>, so that
    ///     band is exactly what cannot be known from it and no tighter tolerance is reachable. It
    ///     is also less than one whole chart, worth about 373 at the bottom of a pool that size, so
    ///     a pool genuinely missing charts still fails. The realistic plate spread — Talented Game
    ///     to Ultimate Game — is 0.012 × 260 × 50 = 156, which this clears with room.
    /// </summary>
    public const double Tolerance = 270;

    /// <summary>The fifty a pool is made of.</summary>
    private const int PoolSize = 50;

    /// <summary>
    ///     The pool those rows add up to: each priced at the plate its score most plausibly carries,
    ///     the best fifty summed. Never rounded — a pool is fifty fractional contributions and the
    ///     comparison is against a number printed to the cent.
    /// </summary>
    public static double Rebuild(ChartType chartType, IEnumerable<(int Level, int Score)> rows)
    {
        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        return rows
            .Select(r => scoring.GetScore(chartType, DifficultyLevel.From(r.Level), PhoenixScore.From(r.Score),
                ScoringConfiguration.ExpectedPlateForScore(PhoenixScore.From(r.Score))))
            .Where(v => v > 0)
            .OrderByDescending(v => v)
            .Take(PoolSize)
            .Sum();
    }

    /// <summary>
    ///     Whether a rebuild is close enough to the published pool to say the mirror holds that
    ///     player's fifty. A rebuild that overshoots is believed too — it can only mean our plate
    ///     expectation ran a little rich, never that we invented charts.
    /// </summary>
    public static bool Confirms(double rebuilt, double publishedPool)
    {
        return publishedPool - rebuilt <= Tolerance;
    }
}
