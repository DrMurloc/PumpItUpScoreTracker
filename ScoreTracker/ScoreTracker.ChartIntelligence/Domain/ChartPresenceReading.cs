using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     Reads one chart's census rows into what the graph draws (docs/design/chart-presence-graph.md
///     §1, §5, §8): each title's share and spots, the title most of its players hold it on, how it rates
///     against its folder, where the viewer stands, and which titles a crowded ranking undercounts.
/// </summary>
internal static class ChartPresenceReading
{
    /// <summary>How far apart, in spots, a chart and its folder must sit before it rates higher or lower.</summary>
    public const int SpotsApartToRate = 5;

    /// <summary>Spots the rest of the folder must take on a title before the comparison means anything.</summary>
    public const int FolderSpotsToRate = 10;

    /// <summary>Spots the rest of the folder must take on a title before its shadow draws.</summary>
    public const int FolderSpotsToDraw = ChartPresenceCensus.HoldersForABox;

    /// <param name="crowding">The chart's official ranking when it is crowded, or null.</param>
    public static ChartPumbilityPresenceRecord? Read(IReadOnlyList<ChartPresenceColumnRow> columns,
        IReadOnlyList<ChartPresenceRow> rows, DateTimeOffset computedAt, double? viewerPumbility,
        double? viewerSpot, PumbilityPresenceCrowding? crowding = null)
    {
        if (rows.Count == 0) return null;

        // A chart reads the layout it was counted over: everyone, or the PIU Scores accounts alone (D13).
        var countsBoardPlayers = rows[0].CountsBoardPlayers;
        var byColumn = rows.ToDictionary(r => r.Column);
        var ordered = columns.Where(c => c.CountsBoardPlayers == countsBoardPlayers).OrderBy(c => c.Order).ToArray();
        if (ordered.Length == 0) return null;
        var bands = ordered.Select(c => PumbilityBand.ByName(PumbilityPool.Total, c.Band)).ToArray();

        var presence = ordered.Select((column, i) =>
        {
            var band = bands[i];
            var players = column.Players;
            byColumn.TryGetValue(column.Order, out var row);
            return new PumbilityPresenceColumn(column.Band, band?.Gem ?? column.Band, band?.Level, players,
                row?.Holders ?? 0, row?.Spots, row?.Dots ?? Array.Empty<double>(),
                row is { FolderSpots: >= FolderSpotsToDraw } ? row.Folder : null, Rate(players, row),
                // Only official-ranking players can go unseen, and they are whoever a title counts past its accounts.
                crowding != null && players > column.SitePlayers);
        }).ToArray();

        PumbilityPresenceViewer? viewer = null;
        if (viewerPumbility is { } pumbility)
        {
            var column = Array.FindIndex(bands, band => band?.Holds(pumbility) == true);
            if (column >= 0) viewer = new PumbilityPresenceViewer(column, viewerSpot);
        }

        return new ChartPumbilityPresenceRecord(presence, MostHeld(presence), Runs(presence), viewer, computedAt,
            crowding);
    }

    private static PumbilityPresenceRating? Rate(int players, ChartPresenceRow? row)
    {
        if (row is not { Spots: { } spots, Folder: { } folder }) return null;
        if (players < PumbilityBand.MinimumForLevel || row.Holders < ChartPresenceCensus.HoldersForABox ||
            row.FolderSpots < FolderSpotsToRate)
            return null;
        if (folder.Median - spots.Median >= SpotsApartToRate) return PumbilityPresenceRating.Higher;
        return spots.Median - folder.Median >= SpotsApartToRate
            ? PumbilityPresenceRating.Lower
            : PumbilityPresenceRating.Same;
    }

    /// <summary>
    ///     The column with the highest share, more players breaking a tie. A title too thin to read
    ///     with confidence only wins when no other title holds the chart at all.
    /// </summary>
    private static int? MostHeld(IReadOnlyList<PumbilityPresenceColumn> columns)
    {
        var held = columns.Select((column, i) => (column, i)).Where(c => c.column.Holders > 0).ToArray();
        if (held.Length == 0) return null;
        var candidates = held.Any(c => !c.column.IsThin) ? held.Where(c => !c.column.IsThin).ToArray() : held;
        return candidates
            .OrderByDescending(c => c.column.Share)
            .ThenByDescending(c => c.column.Players)
            .First().i;
    }

    /// <summary>
    ///     Per gem, the rating most of its holders sit under — higher winning a tie over the same, and
    ///     the same over lower — then neighbouring gems that agree merged into one run.
    /// </summary>
    private static IReadOnlyList<PumbilityPresenceRun> Runs(IReadOnlyList<PumbilityPresenceColumn> columns)
    {
        var runs = new List<(PumbilityPresenceRating Rating, List<Name> Gems)>();
        foreach (var gem in columns.Where(c => c.Rating != null).GroupBy(c => c.Gem))
        {
            var rating = gem.GroupBy(c => c.Rating!.Value)
                .Select(g => (Rating: g.Key, Holders: g.Sum(c => c.Holders)))
                .OrderByDescending(g => g.Holders)
                .ThenBy(g => g.Rating)
                .First().Rating;
            if (runs.Count > 0 && runs[^1].Rating == rating) runs[^1].Gems.Add(gem.Key);
            else runs.Add((rating, new List<Name> { gem.Key }));
        }

        return runs.Select(r => new PumbilityPresenceRun(r.Rating, r.Gems)).ToArray();
    }
}
