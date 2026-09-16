using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Contracts;

/// <summary>
///     A chart's presence on the PUMBILITY ladder (docs/design/chart-presence-graph.md): one column
///     per title, the title most of its players hold it on, the folder comparison summed up per gem,
///     and where the viewer stands.
/// </summary>
/// <param name="MostHeld">
///     The column with the highest share of its players holding the chart, among titles with
///     <see cref="PumbilityBand.MinimumForLevel" /> players or more while any of those holds it. Null
///     when nobody on any title holds it.
/// </param>
/// <param name="Ratings">
///     The folder comparison per gem, in ladder order: each gem takes the rating most of its
///     holders sit under, and neighbouring gems that agree share one run.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record ChartPumbilityPresenceRecord(
    IReadOnlyList<PumbilityPresenceColumn> Columns,
    int? MostHeld,
    IReadOnlyList<PumbilityPresenceRun> Ratings,
    PumbilityPresenceViewer? Viewer,
    DateTimeOffset ComputedAt);

/// <summary>
///     One title's column: a gem, or one of its levels once the gem is opened.
/// </summary>
/// <param name="Band">The title's name, as <see cref="PumbilityBand" /> names it.</param>
/// <param name="Gem">The gem the title belongs to, which is the title itself for an unopened gem.</param>
/// <param name="Level">The level within the gem, or null for a whole gem.</param>
/// <param name="Players">Everyone on the title the chart is counted over.</param>
/// <param name="Spots">The spread of the holders' spots, or null when nobody holds it here.</param>
/// <param name="Dots">Each holder's spot, when there are too few holders for a box; empty otherwise.</param>
/// <param name="Folder">Where the rest of the chart's folder sits on this title, when there is enough of it to draw.</param>
/// <param name="Rating">How the chart rates against the rest of its folder here, or null without enough to say.</param>
[ExcludeFromCodeCoverage]
public sealed record PumbilityPresenceColumn(
    Name Band,
    Name Gem,
    int? Level,
    int Players,
    int Holders,
    PumbilitySpots? Spots,
    IReadOnlyList<double> Dots,
    PumbilitySpotBox? Folder,
    PumbilityPresenceRating? Rating)
{
    /// <summary>The share of the title's players holding the chart in their top 50.</summary>
    public double Share => Players == 0 ? 0 : (double)Holders / Players;

    /// <summary>Too few players on the title to read the column with confidence.</summary>
    public bool IsThin => Players < PumbilityBand.MinimumForLevel;
}

/// <summary>
///     Spots in a top 50, where #1 is the chart worth the most: <paramref name="Min" /> is the highest
///     place a holder gives it and <paramref name="Max" /> the lowest.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PumbilitySpots(double Min, double P25, double Median, double P75, double Max);

/// <summary>The middle half and the median of a set of spots.</summary>
[ExcludeFromCodeCoverage]
public sealed record PumbilitySpotBox(double P25, double Median, double P75);

public enum PumbilityPresenceRating
{
    Higher,
    Same,
    Lower
}

/// <summary>Neighbouring gems that share a rating against the chart's folder.</summary>
[ExcludeFromCodeCoverage]
public sealed record PumbilityPresenceRun(PumbilityPresenceRating Rating, IReadOnlyList<Name> Gems);

/// <summary>
///     The viewer's column, and the chart's spot in their own top 50 — null when it is not in it.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PumbilityPresenceViewer(int Column, double? Spot);
