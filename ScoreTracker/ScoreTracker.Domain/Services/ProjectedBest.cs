using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Services;

/// <summary>
///     The best a player would hold on a chart after a projected play, kept the way the game's own
///     best card keeps it: the best score and the best plate separately, so the two can come from
///     different plays. An SS+ Superb Game followed by an SSS Marvelous Game leaves an SSS Superb Game.
///     <para>
///         This prices projections and nothing else. It never decides what a record becomes: the
///         ledger's rule is <c>BestAttemptPolicy</c>, where plate only breaks a tie at equal score.
///     </para>
/// </summary>
public static class ProjectedBest
{
    /// <summary>
    ///     The higher score with the better plate. A broken hold has no plate and any pass outranks
    ///     it, so against one — or against nothing held — the projected play stands as it is. A held
    ///     pass with no plate on record reads as Rough Game.
    /// </summary>
    public static (PhoenixScore Score, PhoenixPlate Plate) After(RecordedPhoenixScore? held, PhoenixScore score,
        PhoenixPlate plate)
    {
        if (held is not { Score: { } heldScore, IsBroken: false }) return (score, plate);

        var heldPlate = held.Plate ?? PhoenixPlate.RoughGame;
        return (heldScore > score ? heldScore : score, heldPlate > plate ? heldPlate : plate);
    }

    /// <summary>What the chart would be worth once the projected play is merged into what is held.</summary>
    public static double Value(ScoringConfiguration scoring, Chart chart, PhoenixScore score, PhoenixPlate plate,
        RecordedPhoenixScore? held)
    {
        var (bestScore, bestPlate) = After(held, score, plate);
        return scoring.GetScore(chart, bestScore, bestPlate, false);
    }

    /// <summary>
    ///     The plate a peer estimate brings to the merge. The curve reads a plate off the projected
    ///     score, which is other players' score, so it counts only alongside a score that beats the
    ///     held pass; otherwise the estimate brings Rough Game, which leaves the held plate standing
    ///     and the chart worth exactly what it already is.
    /// </summary>
    public static PhoenixPlate PeerPlate(PhoenixScore projected, RecordedPhoenixScore? held)
    {
        return RaisesScore(projected, held)
            ? ScoringConfiguration.ExpectedPlateForScore(projected)
            : PhoenixPlate.RoughGame;
    }

    /// <summary>
    ///     Whether the projected play would put a new score on the card: it beats the pass held, or
    ///     there is no pass held to beat — nothing at all, or only a broken run. At or below a held
    ///     pass, all a play can change is the plate.
    /// </summary>
    public static bool RaisesScore(PhoenixScore projected, RecordedPhoenixScore? held)
    {
        return held is not { Score: { } heldScore, IsBroken: false } || projected > heldScore;
    }
}
