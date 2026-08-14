using ScoreTracker.Domain.Models.Titles.Phoenix2;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The one formula behind every level-ticked PUMBILITY bar
///     (docs/design/pumbility-levels.md §8): tick each rung whose threshold falls strictly inside
///     the bar's span, positioned proportionally, with the rung the pool stands on marked current.
///     Pure — the pumbility bars all span exactly one gem (<c>LinkLadder</c> floors every rung at
///     the rung below), so the formula yields four even ticks there and naturally yields none where
///     ticks would be wrong: BRONZE's first rung, non-pumbility titles, Phoenix.
/// </summary>
public static class PumbilityLevelMarkers
{
    /// <param name="Fraction">Position along the span, 0..1 exclusive.</param>
    /// <param name="Current">True when the pool stands on this rung — the tick worth brightening.</param>
    public sealed record Tick(Phoenix2PumbilityLevel Level, double Fraction, bool Current);

    // The [P.B] gem spans by title name, floor-linked — BuildList runs LinkLadder, so
    // CompletionFloor here is the rung below. The session bars resolve through this because their
    // model carries only the title's name; everything else already holds its span.
    private static readonly IReadOnlyDictionary<string, (int Floor, int Required)> GemSpans =
        Phoenix2TitleList.BuildList()
            .OfType<Phoenix2PumbilityTitle>()
            .Where(t => t.Pool == PumbilityPool.Total)
            .ToDictionary(t => t.Name.ToString(), t => (t.CompletionFloor, t.CompletionRequired),
                StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<Tick> TicksFor(double floor, double ceiling, double pool)
    {
        if (ceiling <= floor) return Array.Empty<Tick>();
        var standing = Phoenix2PumbilityLevel.From(pool).Index;
        return Phoenix2PumbilityLevel.All
            .Where(r => r.Threshold > floor && r.Threshold < ceiling)
            .Select(r => new Tick(r, (r.Threshold - floor) / (ceiling - floor), r.Index == standing))
            .ToArray();
    }

    /// <summary>
    ///     A total-pool gem title's bar span, or null for every other title — which is the gate
    ///     that keeps the [S]/[D] and Phoenix bars exactly as they are.
    /// </summary>
    public static (int Floor, int Required)? GemSpanFor(string title)
    {
        return GemSpans.TryGetValue(title, out var span) ? span : null;
    }
}
