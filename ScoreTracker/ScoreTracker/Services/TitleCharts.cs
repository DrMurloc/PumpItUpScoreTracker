using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Interface;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Which chart each chart title asks for. Matched the way the title itself is earned —
///     <see cref="ISpecificChartTitle.AppliesToChart" /> — so the card the drawer draws and the
///     chart that completes the title cannot be two different charts. Pure, like
///     <see cref="TitleRails" />: no I/O, so the matching is testable on its own.
/// </summary>
public static class TitleCharts
{
    /// <summary>
    ///     Every title in <paramref name="titles" /> that names one chart, keyed by title name. A title
    ///     whose chart is not in the catalog yet — a song that has not imported — is simply absent,
    ///     and the drawer falls back to the title's sentence.
    /// </summary>
    public static IReadOnlyDictionary<Name, Chart> Resolve(IEnumerable<Title> titles, IEnumerable<Chart> charts)
    {
        // Indexed by song first: a title only ever names one song, so it never needs to look at
        // the other few thousand charts.
        var bySong = charts.GroupBy(c => c.Song.Name.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);

        var resolved = new Dictionary<Name, Chart>();
        foreach (var title in titles)
        {
            if (title is not ISpecificChartTitle chartTitle) continue;
            if (!bySong.TryGetValue(chartTitle.SongName.ToString(), out var songCharts)) continue;

            var chart = songCharts.FirstOrDefault(chartTitle.AppliesToChart);
            if (chart != null) resolved[title.Name] = chart;
        }

        return resolved;
    }
}
