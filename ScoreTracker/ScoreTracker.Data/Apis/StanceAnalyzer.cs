using ScoreTracker.Domain.Records;

namespace ScoreTracker.Data.Apis;

/// <summary>
///     Replays a chart's arrows to measure where the player stands, how far they turn, and
///     whether they ever bracket (docs/design/chart-identity.md §4b).
///     <para>
///         Everything here comes from data piucenter already ships and nobody was reading: each
///         arrow carries a panel, a timestamp and their model's limb assignment, which is enough
///         to reconstruct a stance on every row where both feet are planted. That is the only
///         source in the corpus that can answer "is this a half-double" or "how twisty is it"
///         — the badge vocabulary has no word for either.
///     </para>
///     <para>
///         Limb assignment is a model and it is wrong sometimes; <see cref="StanceProfile.BracketRowShare" />
///         exists precisely because of that, as the veto on badges built from the same guess.
///     </para>
/// </summary>
public static class StanceAnalyzer
{
    // The pad in units where one panel step is 1: x runs left to right across both pads, y is
    // up-positive. Corners sit at the diagonals and the centre between them, which is what makes
    // an angle between two feet mean what it looks like.
    private static readonly (double X, double Y)[] SinglesPad =
    {
        (-1, -1), (-1, 1), (0, 0), (1, 1), (1, -1)
    };

    private static readonly (double X, double Y)[] DoublesPad =
    {
        (-2.5, -1), (-2.5, 1), (-1.5, 0), (-0.5, 1), (-0.5, -1),
        (0.5, -1), (0.5, 1), (1.5, 0), (2.5, 1), (2.5, -1)
    };

    private static readonly IReadOnlySet<int> Mid4 = new HashSet<int> { 3, 4, 5, 6 };
    private static readonly IReadOnlySet<int> Mid6 = new HashSet<int> { 2, 3, 4, 5, 6, 7 };

    /// <summary>Any diagonal at all. Measured, then kept off every chip — see the remarks below.</summary>
    private const double DiagonalDegrees = 44;

    /// <summary>
    ///     Square to the side of the pad. Both feet in one vertical column reaches this, which
    ///     means a vertical drill counts and a horizontal one does not — deliberate, and
    ///     acknowledged by the owner: standing in that column you really are sideways.
    /// </summary>
    private const double SideOnDegrees = 89;

    /// <summary>Past square: the trailing foot has crossed the leading one.</summary>
    private const double CrossedDegrees = 91;

    /// <returns>Null when the chart has no rows with both feet down, so nothing can be measured.</returns>
    public static StanceProfile? Analyze(IReadOnlyList<StepArrow> arrows)
    {
        if (arrows.Count == 0) return null;

        var isDoubles = arrows.Any(a => a.Panel > 4);
        var pad = isDoubles ? DoublesPad : SinglesPad;

        var notes = 0;
        var mid4 = 0;
        var mid6 = 0;
        var rows = 0;
        var bracketRows = 0;
        var repeatedRows = 0;
        IReadOnlyList<int>? previousRow = null;
        var stances = 0;
        var diagonal = 0;
        var sideOn = 0;
        var crossed = 0;

        // Feet persist across rows: a row that moves one foot leaves the other where it was,
        // which is how a stance exists at all on a chart of single arrows.
        (double X, double Y)? left = null;
        (double X, double Y)? right = null;

        foreach (var row in arrows.GroupBy(a => a.Time).OrderBy(g => g.Key))
        {
            rows++;
            var bracketed = false;
            foreach (var limb in row.GroupBy(a => a.Limb))
            {
                var panels = limb.Select(a => a.Panel).Where(p => p >= 0 && p < pad.Length).ToArray();
                if (panels.Length == 0) continue;

                notes += panels.Length;
                mid4 += panels.Count(Mid4.Contains);
                mid6 += panels.Count(Mid6.Contains);
                if (panels.Length >= 2) bracketed = true;

                // A bracket is one foot on two panels, so it stands at their midpoint.
                var spot = (X: panels.Average(p => pad[p].X), Y: panels.Average(p => pad[p].Y));
                if (IsLeft(limb.Key)) left = spot;
                else right = spot;
            }

            // Piucenter's footswitch is "a repeated single panel where the PREDICTED limbs
            // differ", so a footswitch and a jack are the same note pattern and only the limb
            // model separates them. This counts the pattern itself, which owes the model
            // nothing: a chart with no repeated single panels cannot contain a footswitch,
            // however confidently the annotation says otherwise.
            var panelsThisRow = row.Select(a => a.Panel).Distinct().OrderBy(p => p).ToArray();
            if (previousRow is { Count: 1 } && panelsThisRow.Length == 1 && previousRow[0] == panelsThisRow[0])
                repeatedRows++;
            previousRow = panelsThisRow;

            if (bracketed) bracketRows++;
            if (left is not { } l || right is not { } r) continue;
            if (Math.Abs(l.X - r.X) < 1e-9 && Math.Abs(l.Y - r.Y) < 1e-9) continue;

            stances++;
            var tau = Math.Abs(Math.Atan2(r.Y - l.Y, r.X - l.X) * 180 / Math.PI);
            if (tau >= DiagonalDegrees) diagonal++;
            if (tau >= SideOnDegrees) sideOn++;
            if (tau > CrossedDegrees) crossed++;
        }

        if (stances == 0 || notes == 0 || rows == 0) return null;

        return new StanceProfile(
            isDoubles,
            Share(mid4, notes),
            Share(mid6, notes),
            Share(diagonal, stances),
            Share(sideOn, stances),
            Share(crossed, stances),
            Share(bracketRows, rows),
            Share(repeatedRows, rows));
    }

    private static bool IsLeft(string limb)
    {
        return limb.StartsWith("l", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal Share(int part, int whole)
    {
        return Math.Round((decimal)part / whole, 4);
    }
}
