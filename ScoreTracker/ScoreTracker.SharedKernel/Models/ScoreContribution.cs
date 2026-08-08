namespace ScoreTracker.SharedKernel.Models;

/// <summary>
///     One chart's contribution to a rating, split into the three things a player can change.
///     Produced by <see cref="ScoringConfiguration.Decompose" />, whose parts sum to the score
///     it split exactly — this is arithmetic on the formula, not an estimate of it.
/// </summary>
/// <param name="Base">
///     What the chart itself pays: its level, and on Phoenix 2 the bump a Singles chart gets up
///     the base curve. The reference is a bare ×1.00 rather than any grade.
/// </param>
/// <param name="FromGrade">What the score adds on top of that base. Negative below the reference.</param>
/// <param name="FromPlate">
///     What the plate adds. Exactly zero on Phoenix, whose plate modifiers are all 1.0 — the plate
///     you walked away with never entered the number at all.
/// </param>
public readonly record struct ScoreContribution(double Base, double FromGrade, double FromPlate)
{
    public double Total => Base + FromGrade + FromPlate;

    public static ScoreContribution operator +(ScoreContribution a, ScoreContribution b) =>
        new(a.Base + b.Base, a.FromGrade + b.FromGrade, a.FromPlate + b.FromPlate);
}
