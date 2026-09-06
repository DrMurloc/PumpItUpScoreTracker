using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Infrastructure;

/// <summary>
///     Every board player's best published score per chart, held in memory
///     (docs/design/pumbility-overhaul.md §6.14).
///     <para>
///         The mirror is swept weekly and asked on every page: a peer group, a fifty check, a chart
///         dialog's board rows and a ghost rival's standing are the same handful of facts read from
///         different angles. Reading them per request cost seconds — the fifty check alone was two
///         and a half of them per chart type — for data that changes once a week.
///     </para>
///     <para>
///         It never needs invalidating. The set is stamped with the sealed snapshot it was built
///         from, so a sweep produces a new one and the old is dropped; nothing has to remember to
///         evict it, and a second app instance builds its own and is correct by construction. Which
///         snapshot is current is itself re-read at most once a minute, which is as often as a
///         weekly sweep can possibly matter.
///     </para>
///     <para>
///         Every type and level the boards carry, not just what prices into a pool: the same reads
///         answer a chart dialog, and a CO-OP chart has a board even though it has no pool. Measured
///         on the prod-synced database, Phoenix 2 is 191,746 rows and Phoenix 1 124,300, a second or
///         two each — and holding the 5% that is CO-OP is what keeps the answer the one the SQL gave.
///     </para>
///     <para>
///         The rows arrive through <see cref="IOfficialSnapshotRepository.GetEveryChartHistory" />
///         rather than a query of this class's own: the placement table has exactly one reader, so
///         that a supplemented row cannot enter an official reading by an author forgetting a
///         predicate (supplemented-leaderboards.md §7).
///     </para>
/// </summary>
internal sealed class BoardScoreStore
{
    /// <summary>How long the answer to "which snapshot is current" is trusted. The sweep is weekly.</summary>
    private static readonly TimeSpan VersionTtl = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<MixEnum, Loaded> _sets = new();
    private readonly IOfficialSnapshotRepository _snapshots;
    private readonly Dictionary<MixEnum, (int SnapshotId, DateTimeOffset ReadAt)> _versions = new();

    public BoardScoreStore(IOfficialSnapshotRepository snapshots)
    {
        _snapshots = snapshots;
    }

    /// <summary>Players' whole history of one chart type, within a level band.</summary>
    public async Task<IReadOnlyList<PlayerChartHistoryRow>> InLevelRange(MixEnum mix, ChartType chartType,
        IReadOnlyCollection<int> playerIds, int minimumLevel, int maximumLevel,
        CancellationToken cancellationToken)
    {
        if (playerIds.Count == 0 || minimumLevel > maximumLevel) return Array.Empty<PlayerChartHistoryRow>();
        var set = await Current(mix, cancellationToken);
        return set.Read(playerIds,
            row => row.Type == chartType && row.Level >= minimumLevel && row.Level <= maximumLevel);
    }

    /// <summary>The same, bounded by charts instead — what a chart's own board asks for.</summary>
    public async Task<IReadOnlyList<PlayerChartHistoryRow>> OnCharts(MixEnum mix,
        IReadOnlyCollection<int> playerIds, IReadOnlyCollection<Guid> chartIds,
        CancellationToken cancellationToken)
    {
        if (playerIds.Count == 0 || chartIds.Count == 0) return Array.Empty<PlayerChartHistoryRow>();
        var wanted = chartIds as IReadOnlySet<Guid> ?? chartIds.ToHashSet();
        var set = await Current(mix, cancellationToken);
        return set.Read(playerIds, row => wanted.Contains(row.ChartId));
    }

    /// <summary>Builds the set at startup so the first viewer of the day does not pay for it.</summary>
    public async Task Warm(MixEnum mix, CancellationToken cancellationToken)
    {
        await Current(mix, cancellationToken);
    }

    private async Task<Loaded> Current(MixEnum mix, CancellationToken cancellationToken)
    {
        var snapshotId = await CurrentSnapshot(mix, cancellationToken);
        lock (_sets)
        {
            if (_sets.TryGetValue(mix, out var have) && have.SnapshotId == snapshotId) return have;
        }

        // A single flight: the first request after a restart pays the load and everyone arriving
        // during it waits for that one rather than starting their own.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            lock (_sets)
            {
                if (_sets.TryGetValue(mix, out var have) && have.SnapshotId == snapshotId) return have;
            }

            var built = await Build(mix, snapshotId, cancellationToken);
            lock (_sets)
            {
                _sets[mix] = built;
            }

            return built;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    ///     The sealed snapshot the set is stamped with. Zero when the mix has never been swept —
    ///     a real value, and the empty set built under it is the right answer.
    /// </summary>
    private async Task<int> CurrentSnapshot(MixEnum mix, CancellationToken cancellationToken)
    {
        lock (_versions)
        {
            if (_versions.TryGetValue(mix, out var known) && DateTimeOffset.UtcNow - known.ReadAt < VersionTtl)
                return known.SnapshotId;
        }

        var latest = await _snapshots.GetLatestSealed(mix, cancellationToken);
        var id = latest?.Id ?? 0;
        lock (_versions)
        {
            _versions[mix] = (id, DateTimeOffset.UtcNow);
        }

        return id;
    }

    private async Task<Loaded> Build(MixEnum mix, int snapshotId, CancellationToken cancellationToken)
    {
        if (snapshotId == 0) return Loaded.From(snapshotId, Array.Empty<Row>());

        // Official rows only: a supplemented row is our own arithmetic laid over the board, and a
        // peer's evidence has to be what piugame published (D59).
        var rows = await _snapshots.GetEveryChartHistory(mix, PlacementScope.OfficialOnly, cancellationToken);
        return Loaded.From(snapshotId, rows
            .Select(r => new Row(r.PlayerId, r.ChartId, r.Level, r.Score, r.Type))
            .ToArray());
    }

    /// <summary>
    ///     One player's best on one chart. A struct in a flat array rather than objects in a
    ///     dictionary: a couple of hundred thousand of these is a few megabytes laid out end to end
    ///     and several times that as nodes, and the array is what makes a player's history a range
    ///     rather than a lookup per row.
    /// </summary>
    private readonly record struct Row(int PlayerId, Guid ChartId, int Level, int Score, ChartType Type);

    /// <summary>One mix's rows, sorted by player so a player's history is a contiguous range.</summary>
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

        public IReadOnlyList<PlayerChartHistoryRow> Read(IReadOnlyCollection<int> playerIds, Func<Row, bool> keep)
        {
            var result = new List<PlayerChartHistoryRow>();
            foreach (var playerId in playerIds.Distinct())
            {
                if (!_byPlayer.TryGetValue(playerId, out var range)) continue;
                for (var i = range.Start; i < range.Start + range.Count; i++)
                {
                    var row = _rows[i];
                    if (keep(row))
                        result.Add(new PlayerChartHistoryRow(row.PlayerId, row.ChartId, row.Level, row.Score));
                }
            }

            return result;
        }
    }
}
