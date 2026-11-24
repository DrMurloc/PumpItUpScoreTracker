using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.Catalog;

/// <summary>
///     check-new-charts' pre-flight: every song a batch is about to add, as the catalog already holds it. A song
///     piugame calls new can be in PIU Scores already — a RISE song the arcade picked up (v1.02.0's 17, v1.03.0's 5),
///     a RISE patch somebody loaded since the RISE catalog, a returner — and /Admin/BulkAddCharts would create a
///     second copy of it. Prints, per name: every song row it matches (names compared on letters and digits only,
///     so curly and straight quotes meet), its charts per mix with ids and levels, its channel per mix, its ko-KR
///     row, and which other song already holds the Korean title the batch means to use; then every Rise and
///     Phoenix 2 version with the charts stamped with it.
///     <para>
///         Names come from the file CATALOG_PROBE_SONGS names, one per line as <c>English</c> or
///         <c>English&lt;TAB&gt;Korean</c>; without it, from the skill's oracle.json. Read-only, small tables only
///         (Song, Chart, ChartMix, SongMix, MixVersion, SongNameLanguage). Skips unless configured, like every
///         catalog probe.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class NewSongBatchProbeTests
{
    private readonly ITestOutputHelper _output;

    public NewSongBatchProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [CatalogProbeFact]
    public async Task A_batchs_songs_as_the_catalog_already_holds_them()
    {
        var wanted = ReadNames();
        Assert.NotEmpty(wanted);

        await using var connection = new SqlConnection(CatalogProbeConfiguration.ConnectionString);
        await connection.OpenAsync();

        var mixes = new Dictionary<Guid, string>();
        await foreach (var r in Rows(connection, "SELECT [Id], [Name] FROM [scores].[Mix]"))
            mixes[r.GetGuid(0)] = r.GetString(1);

        var songs = new List<(Guid Id, string Name, string Artist, long Duration, decimal? Min, decimal? Max, string Type)>();
        await foreach (var r in Rows(connection,
                           "SELECT [Id], [Name], [Artist], [Duration], [MinBpm], [MaxBpm], [Type] FROM [scores].[Song]"))
            songs.Add((r.GetGuid(0), r.GetString(1), r.IsDBNull(2) ? "" : r.GetString(2), Convert.ToInt64(r.GetValue(3)),
                r.IsDBNull(4) ? null : Convert.ToDecimal(r.GetValue(4)), r.IsDBNull(5) ? null : Convert.ToDecimal(r.GetValue(5)),
                r.GetString(6)));

        var korean = new List<(string English, string Korean)>();
        await foreach (var r in Rows(connection,
                           "SELECT [EnglishSongName], [SongName] FROM [scores].[SongNameLanguage] WHERE [CultureCode] = 'ko-KR'"))
            korean.Add((r.GetString(0), r.GetString(1)));

        var matched = wanted.ToDictionary(w => w.English,
            w => songs.Where(s => Norm(s.Name) == Norm(w.English)).ToList());
        var ids = matched.Values.SelectMany(m => m).Select(s => s.Id).Distinct().ToArray();

        var charts = new List<(Guid SongId, Guid ChartId, string Type, int Level, string? StepArtist, Guid? OriginalMix, Guid? MixId, int? MixLevel, Guid? Version)>();
        var channels = new List<(Guid SongId, Guid MixId, string Channel)>();
        if (ids.Length > 0)
        {
            var inList = string.Join(", ", ids.Select(i => $"'{i}'"));
            await foreach (var r in Rows(connection,
                               "SELECT c.[SongId], c.[Id], c.[Type], c.[Level], c.[StepArtist], c.[OriginalMixId], cm.[MixId], cm.[Level], cm.[AddedInVersionId] " +
                               $"FROM [scores].[Chart] c LEFT JOIN [scores].[ChartMix] cm ON cm.[ChartId] = c.[Id] WHERE c.[SongId] IN ({inList})"))
                charts.Add((r.GetGuid(0), r.GetGuid(1), r.GetString(2), Convert.ToInt32(r.GetValue(3)),
                    r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetGuid(5),
                    r.IsDBNull(6) ? null : r.GetGuid(6), r.IsDBNull(7) ? null : Convert.ToInt32(r.GetValue(7)),
                    r.IsDBNull(8) ? null : r.GetGuid(8)));
            await foreach (var r in Rows(connection,
                               $"SELECT [SongId], [MixId], [Channel] FROM [scores].[SongMix] WHERE [SongId] IN ({inList})"))
                channels.Add((r.GetGuid(0), r.GetGuid(1), r.GetString(2)));
        }

        var versions = new List<(Guid Id, Guid MixId, string Name, DateTimeOffset? Released, int Sort)>();
        await foreach (var r in Rows(connection,
                           "SELECT v.[Id], v.[MixId], v.[Name], v.[ReleaseDate], v.[SortOrder] FROM [scores].[MixVersion] v " +
                           "JOIN [scores].[Mix] m ON m.[Id] = v.[MixId] WHERE m.[Name] IN ('Rise', 'Phoenix2')"))
            versions.Add((r.GetGuid(0), r.GetGuid(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetValue(3) switch
                {
                    DateTimeOffset o => o,
                    DateTime d => new DateTimeOffset(d, TimeSpan.Zero),
                    var other => DateTimeOffset.Parse(other.ToString()!, CultureInfo.InvariantCulture)
                },
                Convert.ToInt32(r.GetValue(4))));
        var stamped = new Dictionary<Guid, int>();
        await foreach (var r in Rows(connection,
                           "SELECT [AddedInVersionId], COUNT(*) FROM [scores].[ChartMix] WHERE [AddedInVersionId] IS NOT NULL GROUP BY [AddedInVersionId]"))
            stamped[r.GetGuid(0)] = r.GetInt32(1);

        var versionName = versions.ToDictionary(v => v.Id, v => $"{mixes[v.MixId]} {v.Name}");
        foreach (var (english, ko) in wanted)
        {
            var hits = matched[english];
            _output.WriteLine($"{english}{(ko is null ? "" : $"  [{ko}]")}: {(hits.Count == 0 ? "NOT IN THE CATALOG" : $"{hits.Count} song row(s)")}");
            foreach (var s in hits)
            {
                _output.WriteLine($"   song {s.Id}  \"{s.Name}\" by {s.Artist}, {s.Type}, {s.Duration / 10_000_000}s, BPM {s.Min?.ToString("0.##") ?? "-"}~{s.Max?.ToString("0.##") ?? "-"}");
                foreach (var mix in charts.Where(c => c.SongId == s.Id && c.MixId != null).GroupBy(c => c.MixId!.Value).OrderBy(g => mixes[g.Key]))
                {
                    var list = mix.OrderBy(c => c.Type == "Single" ? 0 : c.Type == "Double" ? 1 : 2).ThenBy(c => c.MixLevel)
                        .Select(c => $"{Label(c.Type, c.MixLevel!.Value)}{(c.MixLevel != c.Level ? $"(chart {c.Level})" : "")}" +
                                     $"{(c.Version is { } v && versionName.TryGetValue(v, out var vn) ? $"@{vn.Split(' ').Last()}" : "")}={c.ChartId.ToString()[..8]}");
                    _output.WriteLine($"     {mixes[mix.Key]}: {string.Join(" ", list)}");
                }
                var orphans = charts.Where(c => c.SongId == s.Id && c.MixId == null).ToArray();
                if (orphans.Length > 0) _output.WriteLine($"     no mix: {string.Join(" ", orphans.Select(c => $"{Label(c.Type, c.Level)}={c.ChartId.ToString()[..8]}"))}");
                var ch = channels.Where(c => c.SongId == s.Id).Select(c => $"{mixes[c.MixId]}={c.Channel}").ToArray();
                _output.WriteLine($"     channels: {(ch.Length == 0 ? "(none)" : string.Join(", ", ch))}");
                var kr = korean.Where(k => Norm(k.English) == Norm(s.Name)).Select(k => k.Korean).ToArray();
                _output.WriteLine($"     ko-KR: {(kr.Length == 0 ? "(none)" : string.Join(" | ", kr))}");
            }
            if (ko is not null)
            {
                var holders = korean.Where(k => k.Korean == ko && Norm(k.English) != Norm(english)).Select(k => k.English).ToArray();
                if (holders.Length > 0) _output.WriteLine($"   *** the Korean title {ko} already belongs to: {string.Join(", ", holders)}");
            }
        }

        _output.WriteLine("");
        foreach (var v in versions.OrderBy(v => mixes[v.MixId]).ThenBy(v => v.Sort))
            _output.WriteLine($"{mixes[v.MixId]} version {v.Name} (released {v.Released:yyyy-MM-dd}, sort {v.Sort}): {stamped.GetValueOrDefault(v.Id)} chart rows stamped");
    }

    private static string Label(string type, int level) =>
        type == "CoOp" ? $"CO-OPx{level}" : $"{type[0]}{level}";

    private static string Norm(string s) =>
        new(s.Normalize(NormalizationForm.FormKC).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static List<(string English, string? Korean)> ReadNames()
    {
        var path = Environment.GetEnvironmentVariable("CATALOG_PROBE_SONGS");
        if (!string.IsNullOrWhiteSpace(path))
            return File.ReadAllLines(path, Encoding.UTF8)
                .Select(l => l.Trim('﻿').Split('\t'))
                .Where(p => p[0].Trim().Length > 0)
                .Select(p => (p[0].Trim(), p.Length > 1 && p[1].Trim().Length > 0 ? p[1].Trim() : null))
                .ToList();

        var oracle = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".piu-score-tracker", "check-new-charts", "oracle.json");
        Assert.True(File.Exists(oracle), "set CATALOG_PROBE_SONGS to a names file, or run check-new-charts first");
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(oracle, Encoding.UTF8));
        return json.RootElement.GetProperty("songs").EnumerateArray()
            .Select(s => (s.GetProperty("name").ValueKind == System.Text.Json.JsonValueKind.String
                ? s.GetProperty("name").GetString()!
                : s.GetProperty("name").GetProperty("value").GetString()!, (string?)null))
            .ToList();
    }

    private static async IAsyncEnumerable<SqlDataReader> Rows(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 60 };
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) yield return reader;
    }
}
