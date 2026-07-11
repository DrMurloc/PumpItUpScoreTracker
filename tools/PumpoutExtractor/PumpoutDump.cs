using Microsoft.Data.Sqlite;

namespace PumpoutExtractor;

/// <summary>
///     Reads a pump-out-sqlite3-dump database and resolves the state of the catalog
///     at any game version via the `_derived_versionAncestor` chain: for a given
///     version, the newest row along its ancestor chain wins — chart presence
///     (`chartVersion` ops, DELETE = absent), ratings (`chartRatingVersion`), and
///     slot/player labels (`chartLabelVersion` ops).
/// </summary>
public sealed class PumpoutDump
{
    private const long DeleteOperation = 2;

    private static readonly string[] SlotLabels =
        { "EASY", "NORMAL", "HARD", "CRAZY", "FREESTYLE", "NIGHTMARE", "PRACTICE" };

    private static readonly Dictionary<string, int> PlayerLabels = new()
    {
        ["TWO PLAYERS"] = 2, ["THREE PLAYERS"] = 3, ["FOUR PLAYERS"] = 4, ["FIVE PLAYERS"] = 5
    };

    public sealed record Song(long Id, string Title, string Cut, string? ArtistDisplay,
        double? BpmMin, double? BpmMax, IReadOnlyList<string> Categories, IReadOnlyList<string> CardPaths);

    public sealed record ChartInfo(long Id, long SongId, string? StepArtistDisplay);

    public sealed record ChartState(string? Mode, long? Level, string? Slot, int PlayerCount);

    public sealed record Membership(long ChartId, long PumpoutMixId, ChartState LastState);

    public sealed record Debut(long ChartId, MixMap.MixDef Mix, bool Mainline);

    public IReadOnlyDictionary<long, Song> Songs => _songs;
    public IReadOnlyDictionary<long, ChartInfo> Charts => _charts;

    private readonly Dictionary<long, Song> _songs = new();
    private readonly Dictionary<long, ChartInfo> _charts = new();
    private readonly Dictionary<long, string> _modes = new();
    private readonly Dictionary<long, long?> _difficultyValues = new();
    private readonly Dictionary<long, (long MixId, string Title, long SortOrder)> _versions = new();
    private readonly Dictionary<long, long> _mixSortOrders = new();
    private readonly Dictionary<long, List<long>> _ancestors = new();
    private readonly Dictionary<long, List<(long ChartId, long Op)>> _chartOpsByVersion = new();
    private readonly Dictionary<long, List<(long ChartId, long ModeId, long DiffId)>> _ratingsByVersion = new();
    private readonly Dictionary<long, List<(long ChartId, string Label, long Op)>> _labelOpsByVersion = new();
    private readonly List<(long ChartId, long VersionId, long Op)> _allChartOps = new();
    private readonly Dictionary<long, ResolvedVersion> _resolutionCache = new();

    private sealed class ResolvedVersion
    {
        public readonly HashSet<long> Present = new();
        public readonly Dictionary<long, (string Mode, long? Level)> Ratings = new();
        public readonly Dictionary<long, HashSet<string>> Labels = new();
    }

    public PumpoutDump(string dbPath)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        conn.Open();
        Load(conn);
    }

    private void Load(SqliteConnection conn)
    {
        List<T> Query<T>(string sql, Func<SqliteDataReader, T> map)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var r = cmd.ExecuteReader();
            var list = new List<T>();
            while (r.Read()) list.Add(map(r));
            return list;
        }

        foreach (var (id, abbrev) in Query("SELECT modeId, internalAbbreviation FROM mode",
                     r => (r.GetInt64(0), r.GetString(1)))) _modes[id] = abbrev;
        foreach (var (id, value) in Query("SELECT difficultyId, value FROM difficulty",
                     r => (r.GetInt64(0), r.IsDBNull(1) ? (long?)null : r.GetInt64(1)))) _difficultyValues[id] = value;
        foreach (var (id, sort) in Query("SELECT mixId, sortOrder FROM mix",
                     r => (r.GetInt64(0), r.GetInt64(1)))) _mixSortOrders[id] = sort;
        foreach (var (id, mixId, title, sort) in Query("SELECT versionId, mixId, internalTitle, sortOrder FROM version",
                     r => (r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetInt64(3))))
            _versions[id] = (mixId, title, sort);
        foreach (var (v, a) in Query(
                     "SELECT versionId, ancestorId FROM _derived_versionAncestor ORDER BY versionId, ancestorSortOrder",
                     r => (r.GetInt64(0), r.GetInt64(1))))
        {
            if (!_ancestors.TryGetValue(v, out var list)) _ancestors[v] = list = new List<long>();
            list.Add(a);
        }

        var titles = Query("SELECT songId, languageId, title FROM songTitle",
                r => (SongId: r.GetInt64(0), Lang: r.GetInt64(1), Title: r.GetString(2)))
            .Where(t => t.Lang == 22) // English
            .GroupBy(t => t.SongId).ToDictionary(g => g.Key, g => g.First().Title);
        var artists = Query(
                "SELECT sa.songId, a.internalTitle, sa.prefix FROM songArtist sa JOIN artist a ON a.artistId=sa.artistId ORDER BY sa.songId, sa.sortOrder",
                r => (SongId: r.GetInt64(0), Name: r.GetString(1), Prefix: r.GetString(2)))
            .GroupBy(x => x.SongId)
            .ToDictionary(g => g.Key, g => ComposeNames(g.Select(x => (x.Name, x.Prefix))));
        var bpms = Query("SELECT songId, bpmMin, bpmMax FROM songBpm",
                r => (SongId: r.GetInt64(0), Min: r.GetDouble(1), Max: r.GetDouble(2)))
            .GroupBy(x => x.SongId).ToDictionary(g => g.Key, g => g.First());
        var categories = Query(
                "SELECT sc.songId, c.internalTitle FROM songCategory sc JOIN category c ON c.categoryId=sc.categoryId",
                r => (SongId: r.GetInt64(0), Cat: r.GetString(1)))
            .GroupBy(x => x.SongId).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Cat).ToList());
        var cards = Query("SELECT songId, path FROM songCard ORDER BY songId, sortOrder",
                r => (SongId: r.GetInt64(0), Path: r.GetString(1)))
            .GroupBy(x => x.SongId).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Path).ToList());

        foreach (var (id, cutId, internalTitle) in Query("SELECT songId, cutId, internalTitle FROM song",
                     r => (r.GetInt64(0), r.GetInt64(1), r.GetString(2))))
        {
            var cut = cutId switch { 1 => "ShortCut", 2 => "Arcade", 3 => "Remix", 4 => "FullSong", _ => "Arcade" };
            _songs[id] = new Song(id,
                titles.GetValueOrDefault(id, internalTitle).Trim(),
                cut,
                artists.GetValueOrDefault(id),
                bpms.TryGetValue(id, out var b) ? b.Min : null,
                bpms.TryGetValue(id, out var b2) ? b2.Max : null,
                categories.GetValueOrDefault(id, Array.Empty<string>()),
                cards.GetValueOrDefault(id, Array.Empty<string>()));
        }

        var stepmakers = Query(
                "SELECT cs.chartId, sm.internalTitle, cs.prefix FROM chartStepmaker cs JOIN stepmaker sm ON sm.stepmakerId=cs.stepmakerId ORDER BY cs.chartId, cs.sortOrder",
                r => (ChartId: r.GetInt64(0), Name: r.GetString(1), Prefix: r.GetString(2)))
            .GroupBy(x => x.ChartId)
            .ToDictionary(g => g.Key, g => ComposeNames(g.Select(x => (x.Name, x.Prefix))));
        foreach (var (id, songId) in Query("SELECT chartId, songId FROM chart", r => (r.GetInt64(0), r.GetInt64(1))))
            _charts[id] = new ChartInfo(id, songId, stepmakers.GetValueOrDefault(id));

        foreach (var (chartId, versionId, op) in Query("SELECT chartId, versionId, operationId FROM chartVersion",
                     r => (r.GetInt64(0), r.GetInt64(1), r.GetInt64(2))))
        {
            if (!_chartOpsByVersion.TryGetValue(versionId, out var list))
                _chartOpsByVersion[versionId] = list = new List<(long, long)>();
            list.Add((chartId, op));
            _allChartOps.Add((chartId, versionId, op));
        }

        foreach (var (chartId, versionId, modeId, diffId) in Query(
                     "SELECT crv.chartId, crv.versionId, cr.modeId, cr.difficultyId FROM chartRatingVersion crv JOIN chartRating cr ON cr.chartRatingId=crv.chartRatingId",
                     r => (r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3))))
        {
            if (!_ratingsByVersion.TryGetValue(versionId, out var list))
                _ratingsByVersion[versionId] = list = new List<(long, long, long)>();
            list.Add((chartId, modeId, diffId));
        }

        foreach (var (chartId, label, versionId, op) in Query(
                     "SELECT cl.chartId, l.internalTitle, clv.versionId, clv.operationId FROM chartLabelVersion clv JOIN chartLabel cl ON cl.chartLabelId=clv.chartLabelId JOIN label l ON l.labelId=cl.labelId",
                     r => (r.GetInt64(0), r.GetString(1), r.GetInt64(2), r.GetInt64(3))))
        {
            if (!SlotLabels.Contains(label) && label != "ANOTHER" && !PlayerLabels.ContainsKey(label)) continue;
            if (!_labelOpsByVersion.TryGetValue(versionId, out var list))
                _labelOpsByVersion[versionId] = list = new List<(long, string, long)>();
            list.Add((chartId, label, op));
        }
    }

    private static string ComposeNames(IEnumerable<(string Name, string Prefix)> parts)
    {
        var result = "";
        foreach (var (name, prefix) in parts)
        {
            var p = prefix.Trim();
            result = result.Length == 0 ? name : $"{result} {(p.Length == 0 ? "&" : p)} {name}";
        }

        return result;
    }

    private ResolvedVersion ResolveAt(long versionId)
    {
        if (_resolutionCache.TryGetValue(versionId, out var cached)) return cached;
        var state = new ResolvedVersion();
        foreach (var ancestor in _ancestors.GetValueOrDefault(versionId, new List<long>()))
        {
            if (_chartOpsByVersion.TryGetValue(ancestor, out var ops))
                foreach (var (chartId, op) in ops)
                {
                    if (op == DeleteOperation) state.Present.Remove(chartId);
                    else state.Present.Add(chartId);
                }

            if (_ratingsByVersion.TryGetValue(ancestor, out var ratings))
                foreach (var (chartId, modeId, diffId) in ratings)
                    state.Ratings[chartId] = (_modes[modeId], _difficultyValues[diffId]);

            if (_labelOpsByVersion.TryGetValue(ancestor, out var labelOps))
                foreach (var (chartId, label, op) in labelOps)
                {
                    if (!state.Labels.TryGetValue(chartId, out var set))
                        state.Labels[chartId] = set = new HashSet<string>();
                    if (op == DeleteOperation) set.Remove(label);
                    else set.Add(label);
                }
        }

        _resolutionCache[versionId] = state;
        return state;
    }

    /// <summary>
    ///     Versions of a mix, oldest first. Prime JE's versions are appended to Prime's
    ///     (owner decision: JE is Prime).
    /// </summary>
    private List<long> VersionsOf(MixMap.MixDef mix)
    {
        if (mix.PumpoutMixId is null) return new List<long>();
        var pumpoutIds = new List<long> { mix.PumpoutMixId.Value };
        if (mix.EnumName == "Prime") pumpoutIds.Add(MixMap.PrimeJePumpoutId);
        return pumpoutIds
            .SelectMany(mixId => _versions.Where(v => v.Value.MixId == mixId)
                .OrderBy(v => v.Value.SortOrder)
                .Select(v => v.Key))
            .ToList();
    }

    /// <summary>
    ///     Membership = the chart was available at ANY version of the mix ("ever
    ///     available", owner decision — mid-mix removals stay recordable). The reported
    ///     state (level/slot/players) is resolved at the LAST version where the chart
    ///     was present in that mix.
    /// </summary>
    public IEnumerable<Membership> MembershipsOf(MixMap.MixDef mix)
    {
        var lastState = new Dictionary<long, ChartState>();
        foreach (var versionId in VersionsOf(mix))
        {
            var resolved = ResolveAt(versionId);
            foreach (var chartId in resolved.Present)
                lastState[chartId] = BuildState(chartId, resolved, mix);
        }

        return lastState.Select(kv => new Membership(kv.Key, mix.PumpoutMixId!.Value, kv.Value));
    }

    /// <summary>State of every present chart at the FINAL version of a mix (used for prod matching against current catalogs).</summary>
    public IReadOnlyDictionary<long, ChartState> FinalStateOf(MixMap.MixDef mix)
    {
        var versions = VersionsOf(mix);
        if (versions.Count == 0) return new Dictionary<long, ChartState>();
        var resolved = ResolveAt(versions[^1]);
        return resolved.Present.ToDictionary(chartId => chartId, chartId => BuildState(chartId, resolved, mix));
    }

    private ChartState BuildState(long chartId, ResolvedVersion resolved, MixMap.MixDef mix)
    {
        var (mode, level) = resolved.Ratings.TryGetValue(chartId, out var r) ? r : (null, null);
        var labels = resolved.Labels.GetValueOrDefault(chartId);

        string? slot = null;
        if (mix.PreExceedSlots && labels is not null)
        {
            var baseSlots = SlotLabels.Where(labels.Contains).ToList();
            var baseSlot = baseSlots.FirstOrDefault();
            if (baseSlot is not null)
                slot = labels.Contains("ANOTHER") ? $"Another {Capitalize(baseSlot)}" : Capitalize(baseSlot);
            else if (labels.Contains("ANOTHER")) slot = "Another";
        }

        var playerCount = 1;
        if (labels is not null)
            foreach (var (label, count) in PlayerLabels)
                if (labels.Contains(label))
                    playerCount = count;
        if (playerCount == 1 && mode is "C" or "R") playerCount = 2;

        return new ChartState(mode, level, slot, playerCount);
    }

    private static string Capitalize(string upper)
    {
        return upper[0] + upper[1..].ToLowerInvariant();
    }

    /// <summary>Every (mode, level) a chart has ever been rated at, across all versions — the any-era hit-test for duplicate-proof matching.</summary>
    public IReadOnlyDictionary<long, HashSet<(string Mode, long Level)>> AllEraLevels()
    {
        var result = new Dictionary<long, HashSet<(string, long)>>();
        foreach (var (_, ratings) in _ratingsByVersion)
        foreach (var (chartId, modeId, diffId) in ratings)
        {
            var level = _difficultyValues[diffId];
            if (level is null) continue;
            if (!result.TryGetValue(chartId, out var set)) result[chartId] = set = new HashSet<(string, long)>();
            set.Add((_modes[modeId], level.Value));
        }

        return result;
    }

    /// <summary>Last-known (mode, level, slot, players) of a chart across the whole timeline — used for Chart.Level on cut-content inserts.</summary>
    public PumpoutDump.ChartState LastKnownState(long chartId)
    {
        foreach (var mix in MixMap.All.Reverse())
        {
            if (mix.PumpoutMixId is null) continue;
            var versions = VersionsOf(mix);
            for (var i = versions.Count - 1; i >= 0; i--)
            {
                var resolved = ResolveAt(versions[i]);
                if (resolved.Present.Contains(chartId)) return BuildState(chartId, resolved, mix);
            }
        }

        return new ChartState(null, null, null, 1);
    }

    /// <summary>
    ///     Debut mix per chart: earliest non-DELETE appearance, preferring mainline
    ///     mixes (Infinity never wins attribution; JE maps to Prime).
    /// </summary>
    public IReadOnlyDictionary<long, Debut> Debuts()
    {
        var result = new Dictionary<long, Debut>();
        foreach (var group in _allChartOps.Where(o => o.Op != DeleteOperation).GroupBy(o => o.ChartId))
        {
            var appearances = group
                .Select(o =>
                {
                    var (mixId, _, versionSort) = _versions[o.VersionId];
                    if (mixId == MixMap.PrimeJePumpoutId) mixId = 1; // JE is Prime
                    return (MixId: mixId, MixSort: _mixSortOrders[mixId], VersionSort: versionSort);
                })
                .OrderBy(x => x.MixSort).ThenBy(x => x.VersionSort)
                .ToList();
            var mainline = appearances.Where(x => x.MixId != MixMap.InfinityPumpoutId).ToList();
            var pick = mainline.Count > 0 ? mainline[0] : appearances[0];
            if (MixMap.ByPumpoutId.TryGetValue(pick.MixId, out var def))
                result[group.Key] = new Debut(group.Key, def, mainline.Count > 0);
        }

        return result;
    }
}
