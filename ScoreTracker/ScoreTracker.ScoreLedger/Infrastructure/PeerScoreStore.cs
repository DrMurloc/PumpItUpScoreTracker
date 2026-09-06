using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Infrastructure;

/// <summary>
///     Players' passing bests, held in memory, for the two reads that ask about OTHER people
///     (docs/design/pumbility-overhaul.md §6.14).
///     <para>
///         A peer group is dozens to hundreds of players and a folder is a hundred charts, so both
///         reads hand SQL a large IN list, once per folder, on a page that draws several. Held
///         instead, the whole of Phoenix 1 is about a million rows and forty megabytes and every
///         peer question after the load is arithmetic.
///     </para>
///     <para>
///         Held per player, which is the shape both eviction and arrival want: a player who imports
///         has their own slice dropped and rebuilt on next use, a player nobody has asked about yet
///         is fetched when they are, and nobody else moves either way. There is deliberately no
///         whole-set expiry — that would put a multi-second rebuild in front of one unlucky viewer
///         for a staleness that is per player in the first place.
///         <see cref="PeerScoreCacheConsumer" /> is what drops a slice, off the same two events the
///         Pumbility projection cache has always used.
///     </para>
///     <para>
///         ⚠ Eviction rides the in-memory bus, so it reaches only the instance that ran the import.
///         On one instance that is exact; before this app is ever scaled out this store needs a
///         cross-instance signal, or a second instance serves its own stale copy of a peer's scores.
///         The per-player expiry below is a backstop for a missed event, not the mechanism.
///     </para>
/// </summary>
internal sealed class PeerScoreStore
{
    /// <summary>A backstop for an eviction that never arrived. The event is the mechanism.</summary>
    private static readonly TimeSpan Backstop = TimeSpan.FromHours(12);

    /// <summary>
    ///     How long a name and a public flag are trusted. Short, because nothing publishes when a
    ///     player renames or goes private, and the read behind it is a few thousand narrow rows.
    /// </summary>
    private static readonly TimeSpan IdentityTtl = TimeSpan.FromMinutes(5);

    /// <summary>How long the chart dimension is trusted. A content update is not a busy hour.</summary>
    private static readonly TimeSpan ChartsTtl = TimeSpan.FromHours(1);

    private readonly Dictionary<MixEnum, (IReadOnlyDictionary<Guid, Chart> Charts, DateTimeOffset ReadAt)>
        _charts = new();

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;
    private readonly Dictionary<MixEnum, Loaded> _mixes = new();
    private Dictionary<Guid, Player> _identity = new();
    private DateTimeOffset _identityAt = DateTimeOffset.MinValue;

    public PeerScoreStore(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>Named passing bests for a set of players across a set of charts.</summary>
    public async Task<IReadOnlyList<UserPhoenixScore>> OnCharts(MixEnum mix, IReadOnlyCollection<Guid> userIds,
        IReadOnlyCollection<Guid> chartIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0 || chartIds.Count == 0) return Array.Empty<UserPhoenixScore>();
        var wanted = chartIds as IReadOnlySet<Guid> ?? chartIds.ToHashSet();
        // A chart set spans folders, so this one walks the player and tests each row.
        return await Read(mix, userIds, rows => (0, rows.Length), row => wanted.Contains(row.ChartId),
            cancellationToken);
    }

    /// <summary>Named passing bests for a set of players across a level band of one chart type.</summary>
    public async Task<IReadOnlyList<UserPhoenixScore>> InLevelRange(MixEnum mix,
        IReadOnlyCollection<Guid> userIds, ChartType chartType, int minimumLevel, int maximumLevel,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0 || minimumLevel > maximumLevel) return Array.Empty<UserPhoenixScore>();
        // A band is contiguous in the order the rows are kept, so it is found rather than filtered:
        // a folder costs what the folder holds instead of everything the peer has ever passed.
        var from = Ordering(chartType, minimumLevel);
        var through = Ordering(chartType, maximumLevel);
        return await Read(mix, userIds, rows => Span(rows, from, through), null, cancellationToken);
    }

    /// <summary>
    ///     The order a player’s rows are kept in: chart type, then level. Both are small enough to
    ///     pack into one integer, which is what makes a band two binary searches.
    /// </summary>
    private static int Ordering(ChartType type, int level)
    {
        return ((int)type << 8) | (level & 0xFF);
    }

    private static int Ordering(Row row)
    {
        return Ordering(row.Type, row.Level);
    }

    /// <summary>The half-open run of rows whose ordering falls within the band, inclusive of both ends.</summary>
    private static (int Start, int End) Span(Row[] rows, int from, int through)
    {
        var start = LowerBound(rows, from);
        var end = LowerBound(rows, through + 1);
        return (start, end);
    }

    private static int LowerBound(Row[] rows, int key)
    {
        int low = 0, high = rows.Length;
        while (low < high)
        {
            var middle = (low + high) >> 1;
            if (Ordering(rows[middle]) < key) low = middle + 1;
            else high = middle;
        }

        return low;
    }

    /// <summary>
    ///     Drops one player's scores on the mixes named. Their next appearance as somebody's peer
    ///     rebuilds them, which is one player's few hundred rows rather than the mix.
    /// </summary>
    public void Evict(Guid userId, MixEnum? mix)
    {
        lock (_mixes)
        {
            foreach (var (key, loaded) in _mixes)
                if (mix == null || key == mix)
                    loaded.Drop(userId);
        }
    }

    /// <summary>
    ///     Loads a whole mix ahead of anyone asking, so the first viewer after a deploy pays for
    ///     nobody. Reads never wait on this: a viewer who arrives mid-warm fetches the peers they
    ///     asked about and the warm fills in the rest around them.
    /// </summary>
    public async Task Warm(MixEnum mix, CancellationToken cancellationToken)
    {
        var loaded = Mix(mix);
        var startedAt = DateTimeOffset.UtcNow;
        var fetched = await Fetch(mix, null, cancellationToken);
        loaded.Put(fetched.Keys.ToArray(), fetched, startedAt);
    }

    private async Task<IReadOnlyList<UserPhoenixScore>> Read(MixEnum mix, IReadOnlyCollection<Guid> userIds,
        Func<Row[], (int Start, int End)> span, Func<Row, bool>? keep, CancellationToken cancellationToken)
    {
        var loaded = Mix(mix);
        var missing = userIds.Distinct().Where(id => !loaded.Holds(id)).ToArray();
        if (missing.Length > 0)
        {
            // One query for everyone the store has not got, not one per player: after an import
            // that is a single player, and on a cold store it is the peer group.
            var startedAt = DateTimeOffset.UtcNow;
            loaded.Put(missing, await Fetch(mix, missing, cancellationToken), startedAt);
        }

        var identity = await Identity(userIds, cancellationToken);
        return loaded.Read(userIds, identity, span, keep);
    }

    private Loaded Mix(MixEnum mix)
    {
        lock (_mixes)
        {
            if (!_mixes.TryGetValue(mix, out var loaded)) _mixes[mix] = loaded = new Loaded();
            return loaded;
        }
    }

    /// <summary>
    ///     Names and public flags for everyone. Re-read whole on expiry rather than per player: it
    ///     is one narrow row each, and a rename has to reach every surface that quotes the name.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, Player>> Identity(IReadOnlyCollection<Guid> needed,
        CancellationToken cancellationToken)
    {
        var known = _identity;
        if (DateTimeOffset.UtcNow - _identityAt < IdentityTtl && needed.All(known.ContainsKey)) return known;

        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await database.User
            .Select(u => new { u.Id, u.Name, u.IsPublic })
            .ToArrayAsync(cancellationToken);
        var built = rows.ToDictionary(u => u.Id, u => new Player(Name.From(u.Name), u.IsPublic));
        _identity = built;
        _identityAt = DateTimeOffset.UtcNow;
        return built;
    }

    /// <summary>
    ///     Passing bests only, every type and level: the chart-set read is asked about whatever the
    ///     viewer holds, which includes CO-OP and charts under any pool floor. A broken row is a
    ///     walkoff in the distribution and never enters — the contract both reads always had.
    ///     <para>
    ///         One table, no joins, read straight off a data reader into the structs that are kept.
    ///         Level and type come from the chart dimension instead, which is four thousand rows
    ///         held beside the scores rather than two columns repeated a million times down the
    ///         wire; materialised the obvious way — joined, projected into objects — a mix was
    ///         sixteen seconds and a few hundred megabytes of garbage, which is the cost this
    ///         store exists to stop paying.
    ///     </para>
    ///     <para>
    ///         A chart the dimension has never heard of is dropped, which is what the join used to
    ///         do: a record on a chart that is not in this mix is not this mix’s evidence.
    ///     </para>
    /// </summary>
    private async Task<Dictionary<Guid, List<Row>>> Fetch(MixEnum mix, IReadOnlyCollection<Guid>? userIds,
        CancellationToken cancellationToken)
    {
        var fetched = new Dictionary<Guid, List<Row>>();
        var ids = userIds?.Distinct().ToArray();
        if (ids is { Length: 0 }) return fetched;

        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var charts = await Charts(mix, database, cancellationToken);

        await database.Database.OpenConnectionAsync(cancellationToken);
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandTimeout = 180;
        Add(command, "@mix", MixIds.For(mix));
        if (ids == null)
        {
            // Everyone. Driven through ChartMix so the plan seeks the record table by chart
            // instead of scanning every mix ever played: the same read without the join took
            // five minutes rather than fifteen seconds.
            command.CommandText = """
                                  SELECT pr.UserId, pr.ChartId, pr.Score, pr.Plate, pr.RecordedDate
                                  FROM scores.ChartMix cm
                                  JOIN scores.Chart c ON cm.ChartId = c.Id
                                  JOIN scores.PhoenixRecord pr ON c.Id = pr.ChartId
                                  WHERE cm.MixId = @mix AND pr.MixId = @mix
                                    AND pr.Score IS NOT NULL AND pr.IsBroken = 0
                                  """;
        }
        else
        {
            // A named few, which the unique index over player, chart and mix seeks directly.
            var names = new string[ids.Length];
            for (var i = 0; i < ids.Length; i++)
            {
                names[i] = $"@u{i}";
                Add(command, names[i], ids[i]);
            }

            command.CommandText = $"""
                                   SELECT pr.UserId, pr.ChartId, pr.Score, pr.Plate, pr.RecordedDate
                                   FROM scores.PhoenixRecord pr
                                   WHERE pr.MixId = @mix AND pr.Score IS NOT NULL AND pr.IsBroken = 0
                                     AND pr.UserId IN ({string.Join(", ", names)})
                                   """;
        }

        // The plate takes a handful of spellings across a million rows, so it is interned into its
        // parsed form once instead of parsed per row.
        var plates = new Dictionary<string, PhoenixPlate?>(StringComparer.Ordinal);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var chartId = reader.GetGuid(1);
            if (!charts.TryGetValue(chartId, out var chart)) continue;

            var userId = reader.GetGuid(0);
            if (!fetched.TryGetValue(userId, out var rows)) fetched[userId] = rows = new List<Row>();

            var plateText = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            if (!plates.TryGetValue(plateText, out var plate))
                plates[plateText] = plate = PhoenixPlateHelperMethods.TryParse(plateText);

            rows.Add(new Row(chartId, reader.GetInt32(2),
                reader.GetFieldValue<DateTimeOffset>(4).UtcTicks, chart.Level, plate, chart.Type));
        }

        return fetched;
    }

    /// <summary>
    ///     Which charts the mix has, and what level and type each is. Rebuilt on the hour: charts
    ///     arrive with a content update rather than with play, so an hour behind is a chart whose
    ///     first scores have not been imported yet either.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, Chart>> Charts(MixEnum mix, ChartAttemptDbContext database,
        CancellationToken cancellationToken)
    {
        lock (_charts)
        {
            if (_charts.TryGetValue(mix, out var known) && DateTimeOffset.UtcNow - known.ReadAt < ChartsTtl)
                return known.Charts;
        }

        var mixId = MixIds.For(mix);
        var rows = await (from cm in database.ChartMix
            join c in database.Chart on cm.ChartId equals c.Id
            where cm.MixId == mixId
            select new { c.Id, cm.Level, c.Type }).ToArrayAsync(cancellationToken);

        var built = (IReadOnlyDictionary<Guid, Chart>)rows.ToDictionary(r => r.Id,
            r => new Chart((short)r.Level, Enum.Parse<ChartType>(r.Type)));
        lock (_charts)
        {
            _charts[mix] = (built, DateTimeOffset.UtcNow);
        }

        return built;
    }

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    /// <summary>
    ///     One passing best. The recorded instant rides as UTC ticks rather than a DateTimeOffset so
    ///     a million of these stay narrow; nothing reads the offset — it is a tie break between
    ///     instants, never a wall clock (see <see cref="UserPhoenixScore" />).
    /// </summary>
    private readonly record struct Row(Guid ChartId, int Score, long RecordedTicks, short Level,
        PhoenixPlate? Plate, ChartType Type);

    private readonly record struct Player(Name Name, bool IsPublic);

    /// <summary>What a chart is, in this mix. The level is per mix; the type is not.</summary>
    private readonly record struct Chart(short Level, ChartType Type);

    /// <summary>What one player holds, and when the store last asked.</summary>
    private readonly record struct Slice(Row[] Rows, DateTimeOffset LoadedAt);

    /// <summary>One mix's scores, per player.</summary>
    private sealed class Loaded
    {
        private static readonly Row[] None = Array.Empty<Row>();

        /// <summary>When a player was dropped, so a fetch that started before it cannot undo it.</summary>
        private readonly Dictionary<Guid, DateTimeOffset> _evictedAt = new();

        private readonly Dictionary<Guid, Slice> _players = new();

        public bool Holds(Guid userId)
        {
            lock (_players)
                return _players.TryGetValue(userId, out var slice)
                       && DateTimeOffset.UtcNow - slice.LoadedAt <= Backstop;
        }

        public void Drop(Guid userId)
        {
            lock (_players)
            {
                _players.Remove(userId);
                _evictedAt[userId] = DateTimeOffset.UtcNow;
            }
        }

        /// <summary>
        ///     Stores these players wholesale. A player in <paramref name="requested" /> with no rows
        ///     is stored empty rather than left out — "they have passed nothing" is an answer, and
        ///     leaving them out would re-query them on every read forever. A player who imported
        ///     while the fetch was running is skipped: their rows are older than the eviction that
        ///     was the point of dropping them.
        /// </summary>
        public void Put(IReadOnlyCollection<Guid> requested, Dictionary<Guid, List<Row>> fetched,
            DateTimeOffset startedAt)
        {
            var loadedAt = DateTimeOffset.UtcNow;
            lock (_players)
            {
                foreach (var userId in requested)
                {
                    if (_evictedAt.TryGetValue(userId, out var evicted) && evicted > startedAt) continue;
                    _evictedAt.Remove(userId);
                    _players[userId] = new Slice(
                        fetched.TryGetValue(userId, out var rows) ? Sorted(rows) : None, loadedAt);
                }
            }
        }

        private static Row[] Sorted(List<Row> rows)
        {
            var array = rows.ToArray();
            Array.Sort(array, (a, b) => Ordering(a).CompareTo(Ordering(b)));
            return array;
        }

        public IReadOnlyList<UserPhoenixScore> Read(IReadOnlyCollection<Guid> userIds,
            IReadOnlyDictionary<Guid, Player> identity, Func<Row[], (int Start, int End)> span,
            Func<Row, bool>? keep)
        {
            // The arrays are never mutated once stored, so they are collected under the lock and
            // read outside it — a folder's filter has no business holding up an import's eviction.
            var held = new List<(Guid UserId, Row[] Rows)>();
            lock (_players)
            {
                foreach (var userId in userIds.Distinct())
                    if (_players.TryGetValue(userId, out var slice))
                        held.Add((userId, slice.Rows));
            }

            var result = new List<UserPhoenixScore>();
            foreach (var (userId, rows) in held)
            {
                // An account the identity read has never seen is treated as private, which is the
                // safe direction: it masks a name rather than publishing one it cannot vouch for.
                var player = identity.TryGetValue(userId, out var known) ? known : new Player(Anonymous, false);
                var (start, end) = span(rows);
                for (var i = start; i < end; i++)
                {
                    var row = rows[i];
                    if (keep != null && !keep(row)) continue;
                    result.Add(new UserPhoenixScore(userId, row.ChartId,
                        player.IsPublic ? player.Name : Anonymous, PhoenixScore.From(row.Score), row.Plate,
                        false, player.IsPublic, new DateTimeOffset(row.RecordedTicks, TimeSpan.Zero)));
                }
            }

            return result;
        }

        /// <summary>The mask the SQL reads have always written for a private player.</summary>
        private static readonly Name Anonymous = Name.From("Anonymous");
    }
}
