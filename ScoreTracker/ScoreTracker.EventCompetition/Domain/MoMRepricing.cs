using ScoreTracker.Domain.Models;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Domain;

/// <summary>
///     The season-comparison counterfactual (docs/design/march-of-murlocs.md §11.3, D20).
///     Each season freezes its own chart balance <i>and</i> its own scoring tables at
///     creation, so the same session is worth different totals in different seasons, and a
///     raw delta between two seasons silently mixes "I got better" with "the game changed".
///     The fix is to re-price the old session — the same charts, the same scores — under the
///     new season's whole frozen configuration, and split what moved: the §4 arithmetic run
///     four times per chart (old/new balance × old/new tables). Needs no new data; both
///     configurations and both snapshots are stored per season.
/// </summary>
internal static class MoMRepricing
{
    /// <param name="oldSession">The older session's chart rows.</param>
    /// <param name="storedOldTotal">The total that season recorded for it, printed verbatim.</param>
    /// <param name="oldSeason">The older board's frozen configuration, snapshot included.</param>
    /// <param name="newSeason">The newer board's frozen configuration, snapshot included.</param>
    public static MoMRepricingSplit Split(IReadOnlyList<MoMSessionChart> oldSession, int storedOldTotal,
        TournamentConfiguration oldSeason, TournamentConfiguration newSeason)
    {
        var oldTables = oldSeason.Scoring;
        var newTables = newSeason.Scoring;
        // Per-chart truncation, exactly as a session stores its rows (TournamentSession.Add
        // casts each chart's points to int), so re-running a session under its own season
        // reproduces its stored total to the point.
        var recomputedOld = Total(oldSession, oldTables);
        var newBalanceOnly = Total(oldSession, WithSnapshot(oldTables, newTables.ChartLevelSnapshot));
        var newTablesOnly = Total(oldSession, WithSnapshot(newTables, oldTables.ChartLevelSnapshot));
        var repriced = Total(oldSession, newTables);
        return new MoMRepricingSplit(
            storedOldTotal,
            recomputedOld,
            newBalanceOnly - recomputedOld,
            newTablesOnly - recomputedOld,
            // Anchored on the stored total: a catalog that moved under the old session shifts
            // every recomputation alike, so the difference is still the two seasons' doing.
            storedOldTotal + (repriced - recomputedOld));
    }

    private static int Total(IReadOnlyList<MoMSessionChart> charts, ScoringConfiguration scoring)
    {
        return charts.Sum(c => (int)scoring.GetScore(c.Chart, c.Score, c.Plate, c.IsBroken));
    }

    /// <summary>
    ///     The same tables over a different balance. A copy, never a mutation: a board's
    ///     configuration is cached and shared, and swapping its snapshot in place would
    ///     re-price every other reader of that board.
    /// </summary>
    internal static ScoringConfiguration WithSnapshot(ScoringConfiguration source,
        IDictionary<Guid, double>? snapshot)
    {
        return new ScoringConfiguration
        {
            ChartLevelSnapshot = snapshot,
            LevelRatings = new Dictionary<DifficultyLevel, int>(source.LevelRatings),
            SongTypeModifiers = new Dictionary<SongType, double>(source.SongTypeModifiers),
            ChartTypeModifiers = new Dictionary<ChartType, double>(source.ChartTypeModifiers),
            LetterGradeModifiers = new Dictionary<PhoenixLetterGrade, double>(source.LetterGradeModifiers),
            PlateModifiers = new Dictionary<PhoenixPlate, double>(source.PlateModifiers),
            SinglesLetterGradeModifiers = source.SinglesLetterGradeModifiers == null
                ? null
                : new Dictionary<PhoenixLetterGrade, double>(source.SinglesLetterGradeModifiers),
            SinglesPlateModifiers = source.SinglesPlateModifiers == null
                ? null
                : new Dictionary<PhoenixPlate, double>(source.SinglesPlateModifiers),
            PgLetterGradeModifier = source.PgLetterGradeModifier,
            CoOpBaseRating = source.CoOpBaseRating,
            Mix = source.Mix,
            MinimumScore = source.MinimumScore,
            ChartModifiers = new Dictionary<Guid, double>(source.ChartModifiers),
            StageBreakModifier = source.StageBreakModifier,
            CustomAlgorithm = source.CustomAlgorithm,
            Formula = source.Formula,
            AdjustToTime = source.AdjustToTime,
            ContinuousLetterGradeScale = source.ContinuousLetterGradeScale
        };
    }
}
