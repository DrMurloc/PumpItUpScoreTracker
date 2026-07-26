using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services.HomeDashboard;

namespace ScoreTracker.Web.Components.HomeWidgets;

/// <summary>
///     How many folders each cell size holds, and which folders a fresh widget starts with.
///     Shared by the widget and its config panel so a freshly-added widget and the panel that
///     opens on it never disagree about either.
/// </summary>
public static class FolderLevelsDefaults
{
    /// <summary>
    ///     Folders a size can show. Fixed per size rather than scrolling: the point of the ladder
    ///     is that a bigger cell shows more folders, not the same folders with a scrollbar.
    /// </summary>
    public static int CapacityFor(SizePreset size) => size.Token switch
    {
        "1x1" => 1,
        "2x1" => 2,
        "2x2" => 4,
        "2x3" => 6,
        "4x3" => 8,
        _ => 4
    };

    /// <summary>
    ///     The folders a widget starts on: your own level, outward, alternating singles and
    ///     doubles so both disciplines are represented from the first render. Each type walks its
    ///     own competitive level — a player two levels stronger in singles gets folders that say
    ///     so — and the walk goes up before down, because the folder you are pushing is more
    ///     interesting than the one behind you.
    /// </summary>
    public static List<FolderLevelsTarget> Suggest(int capacity, double singlesCompetitive,
        double doublesCompetitive)
    {
        var singles = Walk(ChartType.Single, singlesCompetitive).GetEnumerator();
        var doubles = Walk(ChartType.Double, doublesCompetitive).GetEnumerator();

        var picks = new List<FolderLevelsTarget>();
        var takeSingles = true;
        while (picks.Count < capacity)
        {
            var source = takeSingles ? singles : doubles;
            var other = takeSingles ? doubles : singles;
            // One discipline running out of levels lets the other keep filling the cell.
            if (!source.MoveNext())
            {
                if (!other.MoveNext()) break;
                picks.Add(other.Current);
            }
            else
            {
                picks.Add(source.Current);
            }

            takeSingles = !takeSingles;
        }

        return picks;
    }

    // Level, then one up, then one down, widening — clamped to the levels the type actually has.
    private static IEnumerable<FolderLevelsTarget> Walk(ChartType type, double competitive)
    {
        var (min, max) = FolderLevels.Range(type);
        var centre = Math.Clamp((int)Math.Floor(competitive), min, max);
        yield return new FolderLevelsTarget { Type = type, Level = centre };

        for (var offset = 1; offset <= max - min; offset++)
        {
            if (centre + offset <= max) yield return new FolderLevelsTarget { Type = type, Level = centre + offset };
            if (centre - offset >= min) yield return new FolderLevelsTarget { Type = type, Level = centre - offset };
        }
    }
}
