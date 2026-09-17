using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>One player the census counts: where they stand on the ladder and their top 50 in order.</summary>
/// <param name="IsBoardPlayer">True for a player the official ranking is the only record of.</param>
internal sealed record PresenceVoice(PeerVoice Voice, double Pumbility, bool IsBoardPlayer,
    IReadOnlyList<PoolSlot> Fifty);

/// <summary>A census column: the title it stands for and everyone standing on it.</summary>
/// <param name="CountsBoardPlayers">
///     The layout the column belongs to: true for the one counted over everyone, false for the one counted
///     over PIU Scores accounts alone, which a chart without an official ranking reads.
/// </param>
/// <param name="SitePlayers">The PIU Scores accounts among <paramref name="Players" />.</param>
internal sealed record ChartPresenceColumnRow(bool CountsBoardPlayers, int Order, Name Band, int Players,
    int SitePlayers);

/// <summary>
///     One chart on one column: its holders' spots, and the spots every other chart of its folder
///     takes on the same title.
/// </summary>
/// <param name="CountsBoardPlayers">
///     False for a chart piugame publishes no ranking for, which no board player can be seen holding; its
///     <paramref name="Column" /> is then a column of the layout counted over PIU Scores accounts alone.
/// </param>
internal sealed record ChartPresenceRow(Guid ChartId, int Column, bool CountsBoardPlayers, int Holders,
    PumbilitySpots? Spots, IReadOnlyList<double> Dots, int FolderSpots, PumbilitySpotBox? Folder);

internal sealed record ChartPresenceCensusResult(IReadOnlyList<ChartPresenceColumnRow> Columns,
    IReadOnlyList<ChartPresenceRow> Rows);

/// <summary>
///     The PUMBILITY presence census (docs/design/chart-presence-graph.md §2–§4): who stands on each
///     title, and for every chart how many of them hold it and where it sits in their top 50s, beside
///     the rest of its folder. Pure — the saga reads the players and hands them in.
/// </summary>
internal static class ChartPresenceCensus
{
    /// <summary>Holders a chart needs on a title before its spots read as a box rather than one by one.</summary>
    public const int HoldersForABox = 5;

    /// <summary>Spots are whole places or the middle of a tie, so every spot is a multiple of a half.</summary>
    private const int BinsPerSpot = 2;

    private static readonly int Bins = PumbilityPeerPools.PoolSize * BinsPerSpot + 1;

    /// <summary>Players a gem needs before it reads as its levels: every level at the floor a level is read at.</summary>
    public static int PlayersToOpenAGem => PumbilityBand.MinimumForLevel * Phoenix2PumbilityLevel.LevelsPerGem;

    /// <summary>
    ///     The columns, weakest title first: each gem whole, or its levels where the gem holds enough
    ///     players to open. The capstone has no levels and never opens.
    /// </summary>
    public static IReadOnlyList<PumbilityBand> Columns(IEnumerable<double> pumbility)
    {
        var standing = pumbility.ToArray();
        var columns = new List<PumbilityBand>();
        foreach (var gem in PumbilityBand.Gems())
        {
            var levels = PumbilityBand.Levels(PumbilityPool.Total)
                .Where(band => band.Gem == gem.Name && band.Level != null)
                .ToArray();
            if (levels.Length > 1 && standing.Count(gem.Holds) >= PlayersToOpenAGem) columns.AddRange(levels);
            else columns.Add(gem);
        }

        return columns;
    }

    /// <summary>
    ///     Each chart's place in a top 50 given in pool order, #1 the chart worth the most. Charts worth
    ///     exactly the same share the middle of the places they fill, so no tie-break decides a spot.
    /// </summary>
    public static IReadOnlyDictionary<Guid, double> Spots(IReadOnlyList<PoolSlot> fifty)
    {
        var spots = new Dictionary<Guid, double>();
        var start = 0;
        while (start < fifty.Count)
        {
            var end = start;
            while (end + 1 < fifty.Count && fifty[end + 1].Rating.Equals(fifty[start].Rating)) end++;
            var spot = (start + end) / 2.0 + 1;
            for (var i = start; i <= end; i++) spots[fifty[i].ChartId] = spot;
            start = end + 1;
        }

        return spots;
    }

    /// <summary>
    ///     Two layouts. The charts piugame ranks are counted over everyone. The charts it does not rank are
    ///     counted over the PIU Scores accounts alone, whose gems open by those accounts: the ranking players
    ///     are what carry a gem past the opening, and a chart they can never be seen holding would otherwise
    ///     read every level of it thin (D11, D13).
    /// </summary>
    public static ChartPresenceCensusResult Take(IReadOnlyCollection<PresenceVoice> voices,
        IReadOnlyDictionary<Guid, Chart> charts, IReadOnlySet<Guid> chartsWithBoards)
    {
        var everyone = Layout(true, voices, charts, chartsWithBoards.Contains);
        var accounts = Layout(false, voices.Where(v => !v.IsBoardPlayer).ToArray(), charts,
            chartId => !chartsWithBoards.Contains(chartId));
        return new ChartPresenceCensusResult(everyone.Columns.Concat(accounts.Columns).ToArray(),
            everyone.Rows.Concat(accounts.Rows).ToArray());
    }

    /// <summary>
    ///     One layout: its columns over the voices given, and a row on them for each chart it writes. A
    ///     folder's spots come from every chart of the folder either way, so a chart's shadow is the whole
    ///     rest of its folder on those voices.
    /// </summary>
    private static ChartPresenceCensusResult Layout(bool countsBoardPlayers,
        IReadOnlyCollection<PresenceVoice> voices, IReadOnlyDictionary<Guid, Chart> charts,
        Func<Guid, bool> writesRowsFor)
    {
        var columns = Columns(voices.Select(v => v.Pumbility));
        var players = new int[columns.Count];
        var sitePlayers = new int[columns.Count];
        var held = new Dictionary<(Guid ChartId, int Column), int[]>();
        var folders = new Dictionary<(ChartType Type, int Level, int Column), int[]>();

        foreach (var voice in voices)
        {
            var column = ColumnOf(columns, voice.Pumbility);
            if (column < 0) continue;
            players[column]++;
            if (!voice.IsBoardPlayer) sitePlayers[column]++;
            foreach (var (chartId, spot) in Spots(voice.Fifty))
            {
                if (!charts.TryGetValue(chartId, out var chart)) continue;
                var bin = (int)Math.Round(spot * BinsPerSpot);
                Histogram(held, (chartId, column))[bin]++;
                Histogram(folders, (chart.Type, (int)chart.Level, column))[bin]++;
            }
        }

        var rows = new List<ChartPresenceRow>();
        var others = new int[Bins];
        foreach (var chart in charts.Values.Where(c => writesRowsFor(c.Id)))
            for (var column = 0; column < columns.Count; column++)
            {
                held.TryGetValue((chart.Id, column), out var own);
                folders.TryGetValue((chart.Type, (int)chart.Level, column), out var folder);
                if (folder == null) continue;

                for (var bin = 0; bin < Bins; bin++) others[bin] = folder[bin] - (own?[bin] ?? 0);
                var holders = own?.Sum() ?? 0;
                var otherSpots = others.Sum();
                if (holders == 0 && otherSpots == 0) continue;

                rows.Add(new ChartPresenceRow(chart.Id, column, countsBoardPlayers, holders,
                    holders == 0 ? null : Spread(own!, holders),
                    holders is > 0 and < HoldersForABox ? Each(own!) : Array.Empty<double>(),
                    otherSpots, otherSpots == 0 ? null : Box(others, otherSpots)));
            }

        return new ChartPresenceCensusResult(
            columns.Select((band, i) =>
                    new ChartPresenceColumnRow(countsBoardPlayers, i, band.Name, players[i], sitePlayers[i]))
                .ToArray(),
            rows);
    }

    /// <summary>The column a pool sum stands on, or -1 under the ladder. Columns never overlap.</summary>
    public static int ColumnOf(IReadOnlyList<PumbilityBand> columns, double pumbility)
    {
        for (var i = 0; i < columns.Count; i++)
            if (columns[i].Holds(pumbility))
                return i;
        return -1;
    }

    private static int[] Histogram<TKey>(IDictionary<TKey, int[]> histograms, TKey key)
    {
        if (!histograms.TryGetValue(key, out var bins)) histograms[key] = bins = new int[Bins];
        return bins;
    }

    private static PumbilitySpots Spread(int[] bins, int count)
    {
        return new PumbilitySpots(At(bins, 0), Quantile(bins, count, .25), Quantile(bins, count, .5),
            Quantile(bins, count, .75), At(bins, count - 1));
    }

    private static PumbilitySpotBox Box(int[] bins, int count)
    {
        return new PumbilitySpotBox(Quantile(bins, count, .25), Quantile(bins, count, .5),
            Quantile(bins, count, .75));
    }

    private static IReadOnlyList<double> Each(int[] bins)
    {
        var spots = new List<double>();
        for (var bin = 0; bin < bins.Length; bin++)
            for (var i = 0; i < bins[bin]; i++)
                spots.Add((double)bin / BinsPerSpot);
        return spots;
    }

    /// <summary>A quantile interpolated between the two spots either side of it, as a sorted list would give it.</summary>
    private static double Quantile(int[] bins, int count, double quantile)
    {
        var position = (count - 1) * quantile;
        var lower = (int)Math.Floor(position);
        var below = At(bins, lower);
        var above = At(bins, Math.Min(lower + 1, count - 1));
        return below + (above - below) * (position - lower);
    }

    /// <summary>The spot at a zero-based rank, counting up from #1.</summary>
    private static double At(int[] bins, int rank)
    {
        var seen = 0;
        for (var bin = 0; bin < bins.Length; bin++)
        {
            seen += bins[bin];
            if (seen > rank) return (double)bin / BinsPerSpot;
        }

        throw new ArgumentOutOfRangeException(nameof(rank), rank, "Past the last spot counted.");
    }
}
