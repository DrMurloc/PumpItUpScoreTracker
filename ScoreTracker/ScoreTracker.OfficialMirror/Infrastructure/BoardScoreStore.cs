using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Infrastructure;

/// <summary>
///     Every board player's published score per chart, one sealed week at a time, held in memory
///     (docs/design/pumbility-overhaul.md §6.14).
///     <para>
///         The mirror is swept weekly and asked on every page: a peer group, a fifty check, a chart
///         dialog's board rows and a ghost rival's standing are the same handful of facts read from
///         different angles. Reading them per request cost seconds — the fifty check alone was two
///         and a half of them per chart type — for data that changes once a week.
///     </para>
///     <para>
///         It never needs invalidating. A set is stamped with the sealed snapshot it was built from
///         and a caller names the snapshot it is reading, so a new sweep is a new set rather than a
///         stale one, and a second app instance builds its own and is correct by construction. The
///         newest two weeks of a mix are held: a request that read the latest week just before a
///         seal still finds its own week, and nothing older lingers.
///     </para>
///     <para>
///         One week and nothing older, so a build reads that week's rows alone however many weeks
///         the mirror has swept. The caller naming the week is what keeps a peer's chart rows and the
///         PUMBILITY board they are judged against the same week. Every type and level the boards
///         carry, not just what prices into a pool: the same reads answer a chart dialog, and a CO-OP
///         chart has a board even though it has no pool.
///     </para>
///     <para>
///         The rows arrive through <see cref="IOfficialSnapshotRepository.GetChartScoresIn" />
///         rather than a query of this class's own: the placement table has exactly one reader, so
///         that a supplemented row cannot enter an official reading by an author forgetting a
///         predicate (supplemented-leaderboards.md §7).
///     </para>
/// </summary>
internal sealed class BoardScoreStore
{
    /// <summary>How many weeks of a mix are held: the latest, and the one a request racing a seal still reads.</summary>
    private const int WeeksHeld = 2;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<(MixEnum Mix, int SnapshotId), Loaded> _sets = new();
    private readonly IOfficialSnapshotRepository _snapshots;

    public BoardScoreStore(IOfficialSnapshotRepository snapshots)
    {
        _snapshots = snapshots;
    }

    /// <summary>Players' scores of one chart type in one sealed week, within a level band.</summary>
    public async Task<IReadOnlyList<PlayerChartScoreRow>> InLevelRange(MixEnum mix, int snapshotId,
        ChartType chartType, IReadOnlyCollection<int> playerIds, int minimumLevel, int maximumLevel,
        CancellationToken cancellationToken)
    {
        if (playerIds.Count == 0 || minimumLevel > maximumLevel) return Array.Empty<PlayerChartScoreRow>();
        var set = await Week(mix, snapshotId, cancellationToken);
        return set.Read(playerIds,
            row => row.Type == chartType && row.Level >= minimumLevel && row.Level <= maximumLevel);
    }

    /// <summary>The same, bounded by charts instead — what a chart's own board asks for.</summary>
    public async Task<IReadOnlyList<PlayerChartScoreRow>> OnCharts(MixEnum mix, int snapshotId,
        IReadOnlyCollection<int> playerIds, IReadOnlyCollection<Guid> chartIds,
        CancellationToken cancellationToken)
    {
        if (playerIds.Count == 0 || chartIds.Count == 0) return Array.Empty<PlayerChartScoreRow>();
        var wanted = chartIds as IReadOnlySet<Guid> ?? chartIds.ToHashSet();
        var set = await Week(mix, snapshotId, cancellationToken);
        return set.Read(playerIds, row => wanted.Contains(row.ChartId));
    }

    /// <summary>Builds the latest sealed week at startup so the first viewer of the day does not pay for it.</summary>
    public async Task Warm(MixEnum mix, CancellationToken cancellationToken)
    {
        var latest = await _snapshots.GetLatestSealed(mix, cancellationToken);
        if (latest != null) await Week(mix, latest.Id, cancellationToken);
    }

    private async Task<Loaded> Week(MixEnum mix, int snapshotId, CancellationToken cancellationToken)
    {
        lock (_sets)
        {
            if (_sets.TryGetValue((mix, snapshotId), out var have)) return have;
        }

        // A single flight: the first request after a restart pays the load and everyone arriving
        // during it waits for that one rather than starting their own.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            lock (_sets)
            {
                if (_sets.TryGetValue((mix, snapshotId), out var have)) return have;
            }

            var built = await Build(snapshotId, cancellationToken);
            lock (_sets)
            {
                _sets[(mix, snapshotId)] = built;
                foreach (var older in _sets.Keys.Where(key => key.Mix == mix)
                             .OrderByDescending(key => key.SnapshotId).Skip(WeeksHeld).ToArray())
                    _sets.Remove(older);
            }

            return built;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Loaded> Build(int snapshotId, CancellationToken cancellationToken)
    {
        // Official rows only: a supplemented row is our own arithmetic laid over the board, and a
        // peer's evidence has to be what piugame published (D59).
        var rows = await _snapshots.GetChartScoresIn(snapshotId, PlacementScope.OfficialOnly, cancellationToken);
        return Loaded.From(snapshotId, rows
            .Select(r => new Row(r.PlayerId, r.ChartId, r.Level, r.Score, r.Type))
            .ToArray());
    }

    /// <summary>
    ///     One player's score on one chart. A struct in a flat array rather than objects in a
    ///     dictionary: a couple of hundred thousand of these is a few megabytes laid out end to end
    ///     and several times that as nodes, and the array is what makes a player's rows a range
    ///     rather than a lookup per row.
    /// </summary>
    private readonly record struct Row(int PlayerId, Guid ChartId, int Level, int Score, ChartType Type);

    /// <summary>One mix's rows, sorted by player so a player's rows are a contiguous range.</summary>
    private sealed class Loaded
    {
        private readonly Dictionary<int, (int Start, int Count)> _byPlayer;
        private readonly Row[] _rows;

        private Loaded(int snapshotId, Row[] rows, Dictionary<int, (int, int)> byPlayer)
        {
            SnapshotId = snapshotId;
            _rows = rows;
            _byPlayer = byPlayer;
        }

        public int SnapshotId { get; }

        public static Loaded From(int snapshotId, Row[] rows)
        {
            Array.Sort(rows, (a, b) => a.PlayerId.CompareTo(b.PlayerId));
            var index = new Dictionary<int, (int, int)>();
            var start = 0;
            for (var i = 1; i <= rows.Length; i++)
                if (i == rows.Length || rows[i].PlayerId != rows[start].PlayerId)
                {
                    index[rows[start].PlayerId] = (start, i - start);
                    start = i;
                }

            return new Loaded(snapshotId, rows, index);
        }

        public IReadOnlyList<PlayerChartScoreRow> Read(IReadOnlyCollection<int> playerIds, Func<Row, bool> keep)
        {
            var result = new List<PlayerChartScoreRow>();
            foreach (var playerId in playerIds.Distinct())
            {
                if (!_byPlayer.TryGetValue(playerId, out var range)) continue;
                for (var i = range.Start; i < range.Start + range.Count; i++)
                {
                    var row = _rows[i];
                    if (keep(row))
                        result.Add(new PlayerChartScoreRow(row.PlayerId, row.ChartId, row.Level, row.Score));
                }
            }

            return result;
        }
    }
}
