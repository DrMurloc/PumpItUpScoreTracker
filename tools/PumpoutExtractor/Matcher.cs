using System.Text.Json;

namespace PumpoutExtractor;

/// <summary>Prod `dev/export` JSON shapes (raw table rows — see docs/API.md caveats).</summary>
public sealed record ProdSong(Guid Id, string Name, string? ImagePath, string Type, long Duration, string? Artist,
    decimal? MinBpm, decimal? MaxBpm);

public sealed record ProdChart(Guid Id, Guid SongId, int Level, string Type, string? StepArtist, Guid OriginalMixId);

public sealed record ProdChartMix(Guid Id, Guid ChartId, Guid MixId, int Level, int? NoteCount);

public sealed class ProdExport
{
    public List<ProdSong> Songs { get; }
    public List<ProdChart> Charts { get; }
    public List<ProdChartMix> ChartMixes { get; }

    public ProdExport(string dir)
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        T Load<T>(string name) => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(dir, name)), opts)!;
        Songs = Load<List<ProdSong>>("songs.json");
        Charts = Load<List<ProdChart>>("charts.json");
        ChartMixes = Load<List<ProdChartMix>>("chartmixes.json");
    }
}

/// <summary>
///     Matches prod songs/charts to pumpout songs/charts so the generated scripts
///     UPDATE what exists and INSERT only what truly doesn't. Four passes:
///     1. XX-anchored song match — normalized (title, cut) + curated aliases, ties
///        broken by chart-shape overlap scoring; charts pair on (mode, level) at
///        XX's final version (prod's XX rows are final-XX state).
///     2. Residual charts of matched songs — pair when the pumpout chart's rating
///        at ANY era overlaps the prod chart's known levels (catches re-rated
///        revivals without inventing duplicates).
///     3. Second-pass song match by name for prod songs with no XX rows (Phoenix
///        revivals of pre-XX cuts), same any-era chart pairing.
///     4. Suspect detection — residual pairs that look like prod misattributions
///        (equal level + equal name-or-mode) are quarantined: reported, never
///        inserted. The old prod import is known to have misfiled some charts.
/// </summary>
public sealed class Matcher
{
    public sealed record Alias(string ProdName, string ProdType, string PumpoutTitle, string PumpoutCut);

    public sealed record SongMatch(ProdSong Prod, PumpoutDump.Song Pumpout);

    public sealed record ChartMatch(ProdChart Prod, long PumpoutChartId);

    public sealed record Suspect(string ProdSong, string ProdChart, string PumpoutSong, string PumpoutChart, string Reason);

    public readonly List<SongMatch> SongMatches = new();
    public readonly List<ChartMatch> ChartMatches = new();
    public readonly List<(string Song, string Chart)> ProdResiduals = new();
    public readonly List<Suspect> Suspects = new();
    public readonly List<string> Notes = new();

    public IReadOnlySet<long> MatchedPumpoutSongIds => _matchedPumpSongs;
    public IReadOnlySet<long> MatchedPumpoutChartIds => _matchedPumpCharts;
    public IReadOnlySet<long> QuarantinedPumpoutChartIds => _quarantinedPumpCharts;
    public IReadOnlySet<long> QuarantinedPumpoutSongIds => _quarantinedPumpSongs;

    private readonly HashSet<long> _matchedPumpSongs = new();
    private readonly HashSet<long> _matchedPumpCharts = new();
    private readonly HashSet<long> _quarantinedPumpCharts = new();
    private readonly HashSet<long> _quarantinedPumpSongs = new();

    /// <summary>
    ///     Lowercased, suffix-stripped, decomposed (FormD), ASCII letters/digits only.
    ///     Dropping non-ASCII entirely is deliberate: prod and pumpout disagree on
    ///     glyphs like the Cyrillic in "verЯ" or the CJK in "(feat. 月下Lia)", and the
    ///     chart-shape scoring disambiguates any collisions this creates.
    /// </summary>
    public static string Normalize(string title)
    {
        var t = title.ToLowerInvariant();
        foreach (var suffix in new[] { "- full song -", "- short cut -", "-full song-", "-short cut-" })
            t = t.Replace(suffix, "");
        t = t.Normalize(System.Text.NormalizationForm.FormD);
        return new string(t.Where(c => c is >= 'a' and <= 'z' or >= '0' and <= '9').ToArray());
    }

    /// <summary>Alias patterns support a trailing '*' = normalized-prefix match (checked BEFORE normalization strips it).</summary>
    private static (string Normalized, bool Prefix) AliasPattern(string raw)
    {
        var trimmed = raw.TrimEnd();
        var prefix = trimmed.EndsWith('*');
        if (prefix) trimmed = trimmed[..^1];
        return (Normalize(trimmed), prefix);
    }

    private static bool AliasHits(string pattern, bool prefix, string normalizedTitle)
    {
        return prefix ? normalizedTitle.StartsWith(pattern, StringComparison.Ordinal) : normalizedTitle == pattern;
    }

    public static string ModeOf(string prodType)
    {
        return prodType switch
        {
            "Single" => "S", "Double" => "D", "SinglePerformance" => "SP", "DoublePerformance" => "DP",
            "CoOp" => "C", "HalfDouble" => "HDB", _ => "?"
        };
    }

    /// <summary>Reverse of <see cref="ModeOf" />; Routine collapses onto CoOp (owner decision).</summary>
    public static string ProdTypeOf(string pumpoutMode)
    {
        return pumpoutMode switch
        {
            "S" => "Single", "D" => "Double", "SP" => "SinglePerformance", "DP" => "DoublePerformance",
            "C" or "R" => "CoOp", "HDB" => "HalfDouble", _ => "Single"
        };
    }

    public static IReadOnlyList<Alias> LoadAliases(string path)
    {
        if (!File.Exists(path)) return Array.Empty<Alias>();
        return JsonSerializer.Deserialize<List<Alias>>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    public Matcher(ProdExport prod, PumpoutDump dump, IReadOnlyList<Alias> aliases)
    {
        var xxFinal = dump.FinalStateOf(MixMap.All.Single(m => m.EnumName == "XX"));
        var allEraLevels = dump.AllEraLevels(); // pumpout chartId -> set of (mode, level) across history

        var pumpByKey = dump.Songs.Values
            .GroupBy(s => $"{Normalize(s.Title)}|{s.Cut}")
            .ToDictionary(g => g.Key, g => g.ToList());
        var pumpChartsBySong = dump.Charts.Values.GroupBy(c => c.SongId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var aliasPatterns = aliases
            .Select(a => (Pattern: AliasPattern(a.ProdName), a.ProdType, a.PumpoutTitle, a.PumpoutCut))
            .ToList();

        Alias? FindAlias(ProdSong song)
        {
            var norm = Normalize(song.Name);
            foreach (var (pattern, prodType, pumpTitle, pumpCut) in aliasPatterns)
                if (prodType == song.Type && AliasHits(pattern.Normalized, pattern.Prefix, norm))
                    return new Alias(song.Name, song.Type, pumpTitle, pumpCut);
            return null;
        }

        var prodChartById = prod.Charts.ToDictionary(c => c.Id);
        var prodChartsBySong = prod.Charts.GroupBy(c => c.SongId).ToDictionary(g => g.Key, g => g.ToList());
        var xxRowsBySong = prod.ChartMixes.Where(cm => cm.MixId == MixMap.XX)
            .Where(cm => prodChartById.ContainsKey(cm.ChartId))
            .GroupBy(cm => prodChartById[cm.ChartId].SongId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var prodLevelsByChart = prod.Charts.ToDictionary(c => c.Id, c => new HashSet<int> { c.Level });
        foreach (var cm in prod.ChartMixes)
            if (prodLevelsByChart.TryGetValue(cm.ChartId, out var set))
                set.Add(cm.Level);

        var matchedProdSongs = new HashSet<Guid>();
        var matchedProdCharts = new HashSet<Guid>();

        bool SameChart(long pumpChartId, ProdChart prodChart)
        {
            if (!allEraLevels.TryGetValue(pumpChartId, out var eras)) return false;
            var mode = ModeOf(prodChart.Type);
            return eras.Any(e => e.Mode == mode && prodLevelsByChart[prodChart.Id].Contains((int)e.Level));
        }

        void PairResiduals(ProdSong prodSong, PumpoutDump.Song pumpSong)
        {
            var prodResidual = prodChartsBySong.GetValueOrDefault(prodSong.Id, new List<ProdChart>())
                .Where(c => !matchedProdCharts.Contains(c.Id)).ToList();
            var pumpResidual = pumpChartsBySong.GetValueOrDefault(pumpSong.Id, new List<PumpoutDump.ChartInfo>())
                .Where(c => !_matchedPumpCharts.Contains(c.Id)).ToList();
            foreach (var pc in pumpResidual)
            {
                var hit = prodResidual.FirstOrDefault(p => !matchedProdCharts.Contains(p.Id) && SameChart(pc.Id, p));
                if (hit is null) continue;
                ChartMatches.Add(new ChartMatch(hit, pc.Id));
                matchedProdCharts.Add(hit.Id);
                _matchedPumpCharts.Add(pc.Id);
            }
        }

        // ---- pass 1: XX-anchored ------------------------------------------------
        foreach (var prodSong in prod.Songs)
        {
            if (!xxRowsBySong.TryGetValue(prodSong.Id, out var xxRows)) continue;
            var prodShape = xxRows.Select(r => (Mode: ModeOf(prodChartById[r.ChartId].Type), r.Level, r.ChartId)).ToList();

            List<PumpoutDump.Song>? candidates = null;
            if (FindAlias(prodSong) is { } alias)
            {
                var (wanted, prefix) = AliasPattern(alias.PumpoutTitle);
                candidates = dump.Songs.Values
                    .Where(s => s.Cut == alias.PumpoutCut && AliasHits(wanted, prefix, Normalize(s.Title)))
                    .ToList();
                if (candidates.Count == 0)
                    Notes.Add($"Alias for '{prodSong.Name}' matched no pumpout song — check aliases.json");
            }

            if (candidates is null || candidates.Count == 0)
                candidates = pumpByKey.GetValueOrDefault($"{Normalize(prodSong.Name)}|{prodSong.Type}");
            if (candidates is null || candidates.Count == 0) continue; // stays a residual; reported below

            List<(string Mode, long Level, long ChartId)> PumpShape(PumpoutDump.Song s) =>
                pumpChartsBySong.GetValueOrDefault(s.Id, new List<PumpoutDump.ChartInfo>())
                    .Where(c => xxFinal.ContainsKey(c.Id))
                    .Select(c => (State: xxFinal[c.Id], c.Id))
                    .Where(x => x.State.Mode is not null && x.State.Level is not null)
                    .Select(x => (x.State.Mode!, x.State.Level!.Value, x.Id))
                    .ToList();

            var best = candidates
                .Where(c => !_matchedPumpSongs.Contains(c.Id))
                .Select(c => (Song: c, Score: PumpShape(c).Count(p => prodShape.Any(x => x.Mode == p.Mode && x.Level == p.Level))))
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();
            if (best.Song is null) continue;
            if (candidates.Count > 1)
                Notes.Add($"'{prodSong.Name}' had {candidates.Count} pumpout candidates; picked songId {best.Song.Id} (overlap {best.Score})");

            SongMatches.Add(new SongMatch(prodSong, best.Song));
            _matchedPumpSongs.Add(best.Song.Id);
            matchedProdSongs.Add(prodSong.Id);

            var pumpShape = PumpShape(best.Song);
            foreach (var (mode, level, prodChartId) in prodShape)
            {
                var hit = pumpShape.FirstOrDefault(p => p.Mode == mode && p.Level == level && !_matchedPumpCharts.Contains(p.ChartId));
                if (hit.ChartId != 0)
                {
                    ChartMatches.Add(new ChartMatch(prodChartById[prodChartId], hit.ChartId));
                    _matchedPumpCharts.Add(hit.ChartId);
                    matchedProdCharts.Add(prodChartId);
                }
            }

            // ---- pass 2: any-era pairing for this song's residuals ----------------
            PairResiduals(prodSong, best.Song);
        }

        // ---- pass 3: name-only match for prod songs with no XX anchor -------------
        foreach (var prodSong in prod.Songs.Where(s => !matchedProdSongs.Contains(s.Id)))
        {
            var candidates = pumpByKey.GetValueOrDefault($"{Normalize(prodSong.Name)}|{prodSong.Type}")
                ?.Where(c => !_matchedPumpSongs.Contains(c.Id)).ToList();
            if (candidates is null || candidates.Count == 0) continue;
            var pick = candidates[0];
            if (candidates.Count > 1)
                Notes.Add($"'{prodSong.Name}' (no XX anchor) had {candidates.Count} candidates; picked songId {pick.Id}");
            SongMatches.Add(new SongMatch(prodSong, pick));
            _matchedPumpSongs.Add(pick.Id);
            matchedProdSongs.Add(prodSong.Id);
            PairResiduals(prodSong, pick);
        }

        // ---- residual + suspect analysis ------------------------------------------
        var prodResidualRows = prod.ChartMixes.Where(cm => cm.MixId == MixMap.XX)
            .Where(cm => prodChartById.ContainsKey(cm.ChartId) && !matchedProdCharts.Contains(cm.ChartId))
            .Select(cm => (Song: prod.Songs.First(s => s.Id == prodChartById[cm.ChartId].SongId),
                Chart: prodChartById[cm.ChartId], cm.Level))
            .ToList();
        foreach (var r in prodResidualRows)
            ProdResiduals.Add((r.Song.Name, $"{ModeOf(r.Chart.Type)}{r.Level}"));

        var pumpResidualCharts = dump.Charts.Values
            .Where(c => xxFinal.ContainsKey(c.Id) && !_matchedPumpCharts.Contains(c.Id))
            .Select(c => (Song: dump.Songs[c.SongId], Chart: c, State: xxFinal[c.Id]))
            .ToList();

        foreach (var pr in pumpResidualCharts)
        foreach (var (prodSongRow, prodChart, prodLevel) in prodResidualRows)
        {
            var sameLevel = pr.State.Level == prodLevel;
            if (!sameLevel) continue;
            var sameName = Normalize(pr.Song.Title) == Normalize(prodSongRow.Name);
            var sameMode = pr.State.Mode == ModeOf(prodChart.Type);
            if (!sameName && !sameMode) continue;
            Suspects.Add(new Suspect(prodSongRow.Name, $"{ModeOf(prodChart.Type)}{prodLevel}",
                pr.Song.Title, $"{pr.State.Mode}{pr.State.Level?.ToString() ?? "??"}",
                sameName ? "same name + level (type/cut mismatch?)" : "same mode + level (misattributed song?)"));
            _quarantinedPumpCharts.Add(pr.Chart.Id);
            if (!_matchedPumpSongs.Contains(pr.Song.Id)) _quarantinedPumpSongs.Add(pr.Song.Id);
        }

        // a matched song with residuals on BOTH sides is a probable type/level misfile:
        // quarantine its pumpout residuals rather than inserting doubles
        foreach (var match in SongMatches)
        {
            var prodRes = prodChartsBySong.GetValueOrDefault(match.Prod.Id, new List<ProdChart>())
                .Where(c => !matchedProdCharts.Contains(c.Id)).ToList();
            if (prodRes.Count == 0) continue;
            var pumpRes = pumpChartsBySong.GetValueOrDefault(match.Pumpout.Id, new List<PumpoutDump.ChartInfo>())
                .Where(c => !_matchedPumpCharts.Contains(c.Id) && !_quarantinedPumpCharts.Contains(c.Id))
                .ToList();
            foreach (var pc in pumpRes)
            {
                var st = xxFinal.TryGetValue(pc.Id, out var s) ? s : null;
                if (st is null) continue; // not XX-visible: safe to insert, prod can't already have it
                Suspects.Add(new Suspect(match.Prod.Name, string.Join("/", prodRes.Select(c => $"{ModeOf(c.Type)}{c.Level}")),
                    match.Pumpout.Title, $"{st.Mode}{st.Level?.ToString() ?? "??"}",
                    "matched song has residual charts on both sides"));
                _quarantinedPumpCharts.Add(pc.Id);
            }
        }
    }
}
