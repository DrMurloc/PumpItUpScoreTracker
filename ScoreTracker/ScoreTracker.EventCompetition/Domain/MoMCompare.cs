using ScoreTracker.EventCompetition.Contracts;

namespace ScoreTracker.EventCompetition.Domain;

/// <summary>
///     The chart-level half of Compare (§11.3): both modes list the charts the two sessions
///     have in common. A total says who won; this says where. Same-board rows lead with the
///     worst gap, a season comparison with the biggest gain — the caller picks with
///     <paramref name="worstFirst" />. A chart appears at most once per session (no repeats
///     on a board), so the join is one-to-one.
/// </summary>
internal static class MoMCompare
{
    public static IReadOnlyList<MoMSharedChart> Shared(IReadOnlyList<MoMSessionChart> mine,
        IReadOnlyList<MoMSessionChart> theirs, bool worstFirst)
    {
        var theirsById = theirs.GroupBy(c => c.Chart.Id).ToDictionary(g => g.Key, g => g.First());
        var shared = mine
            .Where(c => theirsById.ContainsKey(c.Chart.Id))
            .Select(c =>
            {
                var other = theirsById[c.Chart.Id];
                return new MoMSharedChart(c.Chart, c.Score, c.Plate, c.IsBroken, c.SessionScore,
                    other.Score, other.Plate, other.IsBroken, other.SessionScore);
            });
        return (worstFirst ? shared.OrderBy(s => s.Gap) : shared.OrderByDescending(s => s.Gap)).ToArray();
    }
}
