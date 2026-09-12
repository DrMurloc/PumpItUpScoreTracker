using Microsoft.Data.SqlClient;
using ScoreTracker.ExplorationTests.Catalog;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.Pumbility;

/// <summary>
///     Independent check on the weekly Hardmode census (docs/design/hardmode-leaderboard.md).
///     <para>
///         It replicates the rule from raw rows rather than calling the shipped code, on purpose:
///         the census writes one table a week and nothing downstream can tell a wrong list from a
///         right one, so the only useful check is a second implementation that agrees. It prices
///         Phoenix 2 PUMBILITY in SQL, builds every full pool's fifty, weights by slot
///         (51 − slot), applies the cut, and prints the folder table plus any folder asked for.
///     </para>
///     <para>
///         Configure <c>CatalogProbe:ConnectionString</c> or SCORETRACKER_CATALOG_CONNECTION.
///         Read-only — every statement is a SELECT.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class HardmodeCensusProbeTests
{
    /// <summary>Folders printed in full, as "S21" / "D22". Everything else prints in the summary only.</summary>
    private static readonly string[] Detail = { "S21", "S22", "D21", "D22" };

    private readonly ITestOutputHelper _output;

    public HardmodeCensusProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [CatalogProbeFact]
    public async Task Hardmode_cut_per_folder()
    {
        var charts = await ReadCharts();
        var pools = await ReadPools(charts);
        _output.WriteLine($"charts {charts.Count} · pools {pools.Count} " +
                          $"(site {pools.Count(p => !p.Key.StartsWith('B'))} · board {pools.Count(p => p.Key.StartsWith('B'))})");

        // Slot weight, summed across every pool a chart sits in — the three pool kinds each vote,
        // and the holder count beside it counts a player once (design doc §1).
        var points = new Dictionary<Guid, double>();
        var holders = new Dictionary<Guid, HashSet<string>>();
        foreach (var (key, pool) in pools)
        foreach (var slot in pool)
        {
            points[slot.ChartId] = points.GetValueOrDefault(slot.ChartId) + (51 - slot.Place);
            (holders.TryGetValue(slot.ChartId, out var set) ? set : holders[slot.ChartId] = new HashSet<string>())
                .Add(key);
        }

        var folders = charts.Values.GroupBy(c => (c.Type, c.Level)).OrderBy(g => g.Key.Type).ThenBy(g => g.Key.Level);
        var listed = 0;
        foreach (var folder in folders)
        {
            var rows = folder
                .Select(c => (c.ChartId, c.Name, c.ScoringLevel,
                    Points: points.GetValueOrDefault(c.ChartId),
                    Holders: holders.TryGetValue(c.ChartId, out var h) ? h.Count : 0))
                .OrderBy(r => r.Points).ThenByDescending(r => r.ScoringLevel).ThenBy(r => r.Name)
                .ToArray();
            var unheld = rows.Count(r => r.Holders == 0);
            var cut = Cut(rows.Length, unheld);
            listed += cut;
            var tag = (folder.Key.Type == ChartType.Single ? "S" : "D") + folder.Key.Level;
            _output.WriteLine($"{tag,-4} folder {rows.Length,4} · unheld {unheld,4} · cut {cut,4}");
            if (!Detail.Contains(tag)) continue;
            foreach (var (row, i) in rows.Take(cut + 4).Select((r, i) => (r, i)))
                _output.WriteLine($"     {i + 1,3}. {row.Name,-36} pts {row.Points,7:N0} · held by {row.Holders,4}" +
                                  (i < cut ? "  <<HARD" : string.Empty));
        }

        _output.WriteLine($"TOTAL qualifying {listed}");
        Assert.True(listed > 0, "The census produced no charts — check the connection points at a populated database.");
    }

    /// <summary>
    ///     The cut (design doc D1/D2): 25, or a quarter of the folder if that is fewer, or every
    ///     unheld chart when there are more of those — and exactly one from a folder of four or
    ///     fewer, so a two-chart folder is not silently excluded.
    /// </summary>
    private static int Cut(int folderSize, int unheld)
    {
        if (folderSize <= 4) return 1;
        var flat = Math.Min(25, folderSize / 4);
        return Math.Max(flat, unheld);
    }

    private sealed record ChartRow(Guid ChartId, string Name, ChartType Type, int Level, double ScoringLevel);

    private sealed record PoolSlot(Guid ChartId, int Place);

    private static async Task<IReadOnlyDictionary<Guid, ChartRow>> ReadCharts()
    {
        const string sql = """
                           SELECT c.Id, s.Name, c.Type, cm.Level, ISNULL(sl.ScoringLevel, cm.Level)
                           FROM scores.ChartMix cm
                           JOIN scores.Chart c ON c.Id = cm.ChartId
                           JOIN scores.Song s ON s.Id = c.SongId
                           LEFT JOIN scores.ChartScoringLevel sl ON sl.ChartId = c.Id AND sl.MixId = cm.MixId
                           WHERE cm.MixId = @mix AND c.Type IN ('Single','Double') AND cm.Level >= 10
                           """;
        var result = new Dictionary<Guid, ChartRow>();
        await using var reader = await Read(sql);
        while (await reader.ReadAsync())
            result[reader.GetGuid(0)] = new ChartRow(reader.GetGuid(0), reader.GetString(1),
                Enum.Parse<ChartType>(reader.GetString(2)), reader.GetInt32(3), reader.GetDouble(4));
        return result;
    }

    /// <summary>
    ///     Every full fifty on the mix, keyed "U:&lt;user&gt;|&lt;kind&gt;" for a site account and
    ///     "B:&lt;player&gt;|&lt;kind&gt;" for a board player, where kind is Combined/Single/Double.
    ///     A board player already linked to an account is dropped — they vote through their records.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<PoolSlot>>> ReadPools(
        IReadOnlyDictionary<Guid, ChartRow> charts)
    {
        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        var valued = new Dictionary<string, List<(Guid ChartId, ChartType Type, double Value)>>();

        void Add(string who, Guid chartId, int score, PhoenixPlate? plate)
        {
            if (!charts.TryGetValue(chartId, out var chart)) return;
            var value = scoring.GetScore(chart.Type, DifficultyLevel.From(chart.Level), score,
                plate ?? ScoringConfiguration.ExpectedPlateForScore(score));
            if (value <= 0) return;
            (valued.TryGetValue(who, out var list) ? list : valued[who] = new List<(Guid, ChartType, double)>())
                .Add((chartId, chart.Type, value));
        }

        const string siteSql = """
                               SELECT 'U:' + CONVERT(varchar(40), r.UserId), r.ChartId, r.Score, r.Plate
                               FROM scores.PhoenixRecord r
                               WHERE r.MixId = @mix AND r.IsBroken = 0 AND r.Score IS NOT NULL
                               """;
        await using (var reader = await Read(siteSql))
            while (await reader.ReadAsync())
                Add(reader.GetString(0), reader.GetGuid(1), reader.GetInt32(2),
                    reader.IsDBNull(3) ? null : PhoenixPlateHelperMethods.TryParse(reader.GetString(3)));

        // Board pools: the best score the mirror holds per (player, chart) across every snapshot,
        // plate inferred from the score because a board row never carries one.
        const string boardSql = """
                                SELECT 'B:' + CONVERT(varchar(20), p.PlayerId), l.ChartId, MAX(p.Score)
                                FROM scores.OfficialLeaderboardPlacement p
                                JOIN scores.OfficialLeaderboard l ON l.Id = p.LeaderboardId
                                LEFT JOIN scores.OfficialPlayer op ON op.Id = p.PlayerId
                                WHERE l.LeaderboardType = 'Chart' AND l.MixId = @mix AND l.ChartId IS NOT NULL
                                  AND op.UserId IS NULL
                                GROUP BY p.PlayerId, l.ChartId
                                """;
        await using (var reader = await Read(boardSql))
            while (await reader.ReadAsync())
                Add(reader.GetString(0), reader.GetGuid(1), (int)reader.GetDecimal(2), null);

        var pools = new Dictionary<string, IReadOnlyList<PoolSlot>>();
        foreach (var (who, all) in valued)
        {
            AddPool(pools, who + "|Combined", all);
            AddPool(pools, who + "|Single", all.Where(v => v.Type == ChartType.Single));
            AddPool(pools, who + "|Double", all.Where(v => v.Type == ChartType.Double));
        }

        return pools;
    }

    /// <summary>Only a full fifty votes (D6) — a partial pool's slot 1 would carry 50 points off three charts.</summary>
    private static void AddPool(IDictionary<string, IReadOnlyList<PoolSlot>> into, string key,
        IEnumerable<(Guid ChartId, ChartType Type, double Value)> values)
    {
        var fifty = values.OrderByDescending(v => v.Value).ThenBy(v => v.ChartId).Take(50).ToArray();
        if (fifty.Length < 50) return;
        into[key] = fifty.Select((v, i) => new PoolSlot(v.ChartId, i + 1)).ToArray();
    }

    private static async Task<SqlDataReader> Read(string sql)
    {
        var connection = new SqlConnection(CatalogProbeConfiguration.ConnectionString);
        await connection.OpenAsync();
        var command = new SqlCommand(sql, connection) { CommandTimeout = 300 };
        command.Parameters.AddWithValue("@mix", MixThemesPhoenix2Id);
        return await command.ExecuteReaderAsync(System.Data.CommandBehavior.CloseConnection);
    }

    /// <summary>The Phoenix 2 row's id, which the probe reads by value rather than joining scores.Mix.</summary>
    private static readonly Guid MixThemesPhoenix2Id = Guid.Parse("A9B7D3C1-52E8-4F06-9B1A-2F8C33E01948");
}
