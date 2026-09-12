using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using DomainUser = ScoreTracker.Domain.Models.User;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     The mirror's answer to "who is on the board around me, and what did they score" — the read
///     behind PUMBILITY peers drawn from the official boards
///     (docs/design/pumbility-overhaul.md D59–D61, §6.13).
///     <para>
///         It lives here rather than in the reader because the rules are the mirror's own and there
///         should be one copy of them: the projection and the standing ask the same question and
///         must not get different answers. That includes who a board row belongs to — the mirror
///         will not report an account it is not entitled to speak for.
///     </para>
/// </summary>
internal sealed class BoardPeerReader
{
    /// <summary>
    ///     How long the qualified set is held. The mirror is swept weekly and the key carries the
    ///     snapshot, so a new sweep is a new key rather than a stale answer; this only bounds how
    ///     long a dead snapshot's set occupies memory.
    /// </summary>
    private static readonly TimeSpan QualifiedTtl = TimeSpan.FromHours(12);

    /// <summary>
    ///     How long the folded board is held. Everything in it but the window is the same for
    ///     every viewer, so it is prepared once and filtered per person. Five minutes, because one
    ///     thing inside it moves without a sweep — whether a peer's account is public, which is what
    ///     decides whether the mirror names the account behind a row at all (D61) — and that is the
    ///     same fact, held for the same span, as the name and public flag the Ledger's own peer
    ///     store trusts.
    /// </summary>
    private static readonly TimeSpan PreparedTtl = TimeSpan.FromMinutes(5);

    private readonly BoardScoreStore _board;
    private readonly IMemoryCache _cache;
    private readonly IOfficialSnapshotRepository _snapshots;
    private readonly IUserReader _users;

    public BoardPeerReader(IOfficialSnapshotRepository snapshots, IUserReader users, IMemoryCache cache,
        BoardScoreStore board)
    {
        _snapshots = snapshots;
        _users = users;
        _cache = cache;
        _board = board;
    }

    /// <summary>
    ///     The per-type PUMBILITY board a chart type is priced against. Anything but Singles or
    ///     Doubles reads the combined board — a co-op chart has no pool of its own, and the
    ///     combined board is the only honest stand-in.
    /// </summary>
    public static string BoardNameFor(PumbilityPool pool)
    {
        return pool switch
        {
            PumbilityPool.Singles => PumbilityBoards.Singles,
            PumbilityPool.Doubles => PumbilityBoards.Doubles,
            _ => PumbilityBoards.Combined
        };
    }

    /// <summary>
    ///     The ladder a chart type's peers stand on — its own pool, and the merged one for anything
    ///     that has none of its own.
    /// </summary>
    private static PumbilityPool PoolOf(ChartType chartType)
    {
        return chartType switch
        {
            ChartType.Single => PumbilityPool.Singles,
            ChartType.Double => PumbilityPool.Doubles,
            _ => PumbilityPool.Total
        };
    }

    /// <summary>A tag as both spellings write it: the site's NAME #1234 and piugame's NAME#1234.</summary>
    private static string Normalise(string tag)
    {
        return tag.Replace(" ", string.Empty);
    }

    public async Task<BoardPeerGroupReading?> GetBoardPeers(MixEnum mix, ChartType chartType, double minimumPool,
        double maximumPool, Guid? viewerAccountId, CancellationToken cancellationToken)
    {
        if (minimumPool > maximumPool) return null;

        var latest = await _snapshots.GetLatestSealed(mix, cancellationToken);
        if (latest?.CompletedAt == null) return null;
        var asOf = latest.CompletedAt.Value;

        // The window and the viewer are the only parts of this that are about who is asking.
        // Everyone in the band asks the same question of the same board and gets the same fold,
        // so the fold is done once and filtered per person.
        var prepared = await PreparedPeers(mix, PoolOf(chartType), latest.Id, cancellationToken);
        return new BoardPeerGroupReading(asOf, prepared
            // Never yourself (D31). The window is centred on your own pool and the board
            // publishes that same number for you, so your row is inside it essentially always —
            // and if your account is private it comes back naming nobody, which is exactly the
            // shape a caller cannot recognise. Answered here because this is the one place that
            // knows the account behind a row it refuses to name.
            .Where(p => viewerAccountId == null || p.Account != viewerAccountId)
            .Where(p => p.Reading.Pool >= minimumPool && p.Reading.Pool <= maximumPool)
            .Select(p => p.Reading)
            .ToArray());
    }

    /// <summary>
    ///     A folded person, with the account the mirror resolved kept beside the reading rather
    ///     than inside it. <see cref="Account" /> is what the mirror KNOWS; the reading names an
    ///     account only when it is public and may be named (D61). The difference is the whole
    ///     reason a viewer can be excluded from their own board peers without their private link
    ///     ever leaving this class.
    /// </summary>
    /// <summary>
    ///     The board's own players standing on one band of a PUMBILITY ladder (D68): half-open, and
    ///     read off the board that ladder ranks — the combined board for the merged one, the
    ///     per-type boards for the others. A player counts only where the mirror can rebuild their
    ///     whole fifty (D60), which on the merged ladder means both types rebuilt together against
    ///     the combined number. Null when the mix has never swept that board.
    /// </summary>
    public async Task<BoardPeerGroupReading?> GetBoardBand(MixEnum mix, PumbilityPool pool, double floor,
        double? ceiling, Guid? viewerAccountId, CancellationToken cancellationToken)
    {
        var latest = await _snapshots.GetLatestSealed(mix, cancellationToken);
        if (latest?.CompletedAt == null) return null;

        var prepared = await PreparedPeers(mix, pool, latest.Id, cancellationToken);
        return new BoardPeerGroupReading(latest.CompletedAt.Value, prepared
            // Never yourself (D31), answered here because this is the one place that knows the
            // account behind a row it will not name (D61).
            .Where(p => viewerAccountId == null || p.Account != viewerAccountId)
            // Half-open: a pool sitting exactly on the next rung's threshold holds that title.
            .Where(p => p.Reading.Pool >= floor && (ceiling is not { } top || p.Reading.Pool < top))
            .Select(p => p.Reading)
            .ToArray());
    }

    private sealed record PreparedPeer(BoardPeerReading Reading, Guid? Account);

    /// <summary>
    ///     The whole board folded to people: everyone who qualifies (D60), one entry per person,
    ///     their pool the best of the rows they own and their tag the row that reads it. Held for
    ///     <see cref="PreparedTtl" /> and shared by every viewer, because none of it depends on
    ///     who is asking.
    ///     <para>
    ///         The fold happens before the window rather than after, which is also the more honest
    ///         order: a person's pool is their best row, so a person whose best sits above the
    ///         window is above it, even when a lesser row of theirs would have fit inside.
    ///     </para>
    /// </summary>
    private async Task<IReadOnlyList<PreparedPeer>> PreparedPeers(MixEnum mix, PumbilityPool pool,
        int snapshotId, CancellationToken cancellationToken)
    {
        var key = $"{nameof(BoardPeerReader)}__Prepared__{mix}__{pool}__{snapshotId}";
        if (_cache.TryGetValue(key, out IReadOnlyList<PreparedPeer>? cached) && cached != null) return cached;

        var boardName = BoardNameFor(pool);
        var board = (await _snapshots.GetBoards(mix, cancellationToken))
            .FirstOrDefault(b => b.LeaderboardType == LeaderboardTypes.Rating && b.Name == boardName);
        // Phoenix publishes one PUMBILITY board and no per-type split, so asking it for Singles is a
        // legitimate miss rather than a failure — the caller reads "no board peers on this mix".
        if (board == null) return Empty(key);

        // Official rows only: a supplemented row is our own arithmetic laid over the board, and a
        // peer's pool has to be the number piugame published or the window is measuring two things.
        var everyone = await _snapshots.GetBoardPlacements(snapshotId, board.Id,
            PlacementScope.OfficialOnly, cancellationToken);

        // Whose fifty the mirror holds is a property of the PLAYER and the snapshot, not of whoever
        // is looking (D60), so it is answered once for the whole board and shared. Per viewer it was
        // two and a half seconds of rebuild on every cold sweep, twice — once per chart type — for
        // an answer every viewer in the band would have computed identically.
        var qualified = await QualifiedPlayers(mix, pool, snapshotId, everyone, cancellationToken);

        var rows = everyone.Where(p => qualified.Contains(p.PlayerId)).ToArray();
        if (rows.Length == 0) return Empty(key);

        var players = (await _snapshots.GetPlayersByIds(rows.Select(r => r.PlayerId).Distinct().ToArray(),
                cancellationToken))
            .ToDictionary(p => p.Id);

        var claimable = rows.Where(r => players.ContainsKey(r.PlayerId))
            .Select(r => (Row: r, Player: players[r.PlayerId]))
            .ToArray();

        var accounts = await ResolveAccounts(claimable.Select(c => c.Player).ToArray(), cancellationToken);

        // One person per resolved account, and one per unclaimed row. Their pool is the best of the
        // rows they own — a person is at their best — and their tag is the row that reads it.
        var peers = claimable
            .GroupBy(c => accounts.TryGetValue(c.Player.Id, out var account)
                ? account.Id.ToString()
                : $"board:{c.Player.Id}")
            .Select(g =>
            {
                var best = g.OrderByDescending(c => c.Row.Score).ThenBy(c => c.Player.Id).First();
                accounts.TryGetValue(best.Player.Id, out var account);
                return new PreparedPeer(new BoardPeerReading(
                    g.OrderByDescending(c => c.Row.Score).Select(c => c.Player.Id).ToArray(),
                    best.Player.Username,
                    (double)best.Row.Score,
                    // A private account is a board player: named by its public tag, scored from
                    // public rows, and never reported as the account it happens to be (D61).
                    account is { IsPublic: true } ? account.Id : null), account?.Id);
            })
            .ToArray();

        _cache.Set(key, (IReadOnlyList<PreparedPeer>)peers, PreparedTtl);
        return peers;
    }

    private IReadOnlyList<PreparedPeer> Empty(string key)
    {
        var none = (IReadOnlyList<PreparedPeer>)Array.Empty<PreparedPeer>();
        _cache.Set(key, none, PreparedTtl);
        return none;
    }

    /// <summary>
    ///     Every player on this board whose fifty the mirror can rebuild to within the tolerance
    ///     (D60), answered once per snapshot and chart type and shared by every viewer. The check
    ///     runs per BOARD ROW rather than per person: a person's rows are folded later, and a fold
    ///     can only add charts, so a row that qualifies alone still qualifies folded.
    /// </summary>
    private async Task<IReadOnlySet<int>> QualifiedPlayers(MixEnum mix, PumbilityPool pool, int snapshotId,
        IReadOnlyList<PlacementRow> everyone, CancellationToken cancellationToken)
    {
        var key = $"{nameof(BoardPeerReader)}__Qualified__{mix}__{pool}__{snapshotId}";
        if (_cache.TryGetValue(key, out IReadOnlySet<int>? cached) && cached != null) return cached;

        var history = (await PricedRows(mix, pool, everyone.Select(p => p.PlayerId).Distinct().ToArray(),
                cancellationToken))
            .GroupBy(r => r.PlayerId)
            .ToDictionary(g => g.Key, g => g.ToArray());

        var qualified = everyone
            .Where(row => history.TryGetValue(row.PlayerId, out var theirs)
                          && BoardPoolCheck.Confirms(
                              BoardPoolCheck.Rebuild(theirs.Select(r => (r.Type, r.Level, r.Score))),
                              (double)row.Score))
            .Select(row => row.PlayerId)
            .ToHashSet();

        _cache.Set(key, (IReadOnlySet<int>)qualified, QualifiedTtl);
        return qualified;
    }

    /// <summary>
    ///     The rows a pool is rebuilt from: one type's for a typed ladder, and both types' for the
    ///     merged one, whose fifty is drawn from either (D68).
    /// </summary>
    private async Task<IReadOnlyList<(int PlayerId, ChartType Type, int Level, int Score)>> PricedRows(MixEnum mix,
        PumbilityPool pool, IReadOnlyCollection<int> playerIds, CancellationToken cancellationToken)
    {
        var types = pool switch
        {
            PumbilityPool.Singles => new[] { ChartType.Single },
            PumbilityPool.Doubles => new[] { ChartType.Double },
            _ => new[] { ChartType.Single, ChartType.Double }
        };
        var rows = new List<(int PlayerId, ChartType Type, int Level, int Score)>();
        foreach (var type in types)
            rows.AddRange((await _board.InLevelRange(mix, type, playerIds, PeerGroup.PumbilityPoolFloor,
                    DifficultyLevel.Max, cancellationToken))
                .Select(r => (r.PlayerId, type, r.Level, r.Score)));
        return rows;
    }

    /// <summary>
    ///     Board row to the account behind it, by link first and then by game tag with spacing and
    ///     case ignored — the link column is young and the tag is the durable evidence, so the tag
    ///     pass finds accounts the link never caught.
    ///     <para>
    ///         A tag two accounts claim resolves to neither. Guessing would let the mirror name the
    ///         wrong person, and reading the row as a plain board player costs only the fold.
    ///     </para>
    /// </summary>
    private async Task<Dictionary<int, DomainUser>> ResolveAccounts(
        IReadOnlyCollection<PlayerDimension> players, CancellationToken cancellationToken)
    {
        var byPlayer = new Dictionary<int, DomainUser>();

        var linked = players.Where(p => p.UserId != null).ToArray();
        var unlinked = players.Where(p => p.UserId == null).ToArray();

        var found = new List<DomainUser>();
        if (linked.Length > 0)
            found.AddRange(await _users.GetUsers(linked.Select(p => p.UserId!.Value).Distinct(), cancellationToken));
        if (unlinked.Length > 0)
            found.AddRange(await _users.GetUsersByGameTags(
                unlinked.Select(p => p.Username).Distinct().ToArray(), cancellationToken));

        var byId = found.GroupBy(u => u.Id).ToDictionary(g => g.Key, g => g.First());
        var byTag = found
            .Where(u => u.GameTag != null)
            .GroupBy(u => Normalise(u.GameTag!.Value.ToString()), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(u => u.Id).Distinct().Count() == 1)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var player in players)
        {
            if (player.UserId != null && byId.TryGetValue(player.UserId.Value, out var linkedUser))
            {
                byPlayer[player.Id] = linkedUser;
                continue;
            }

            if (byTag.TryGetValue(Normalise(player.Username), out var taggedUser))
                byPlayer[player.Id] = taggedUser;
        }

        return byPlayer;
    }

    public async Task<IReadOnlyList<BoardScoreReading>> GetBoardScores(MixEnum mix, ChartType chartType,
        IReadOnlyCollection<int> boardPlayerIds, int minimumLevel, int maximumLevel,
        CancellationToken cancellationToken)
    {
        if (boardPlayerIds.Count == 0 || minimumLevel > maximumLevel)
            return Array.Empty<BoardScoreReading>();

        var rows = await _board.InLevelRange(mix, chartType, boardPlayerIds, minimumLevel, maximumLevel,
            cancellationToken);

        return rows.Select(r => new BoardScoreReading(r.PlayerId, r.ChartId, r.Level, r.Score)).ToArray();
    }

    public async Task<BoardScoreReadings> GetBoardScoresOn(MixEnum mix,
        IReadOnlyCollection<int> boardPlayerIds, IReadOnlyCollection<Guid> chartIds,
        CancellationToken cancellationToken)
    {
        if (boardPlayerIds.Count == 0 || chartIds.Count == 0) return BoardScoreReadings.None;

        var latest = await _snapshots.GetLatestSealed(mix, cancellationToken);
        if (latest?.CompletedAt == null) return BoardScoreReadings.None;

        // A caller names one id per person — the row their pool was read from. A person who has
        // renamed owns others, and a pass recorded under the old tag is theirs just as much: the
        // projection already counts it, because it is handed the whole set. Without this the two
        // numbers on one card disagree about who passed the chart (bug check 2026-09-06).
        var owner = await Owners(mix, latest.Id, boardPlayerIds, cancellationToken);
        var rows = await _board.OnCharts(mix, owner.Keys.ToArray(), chartIds, cancellationToken);

        // Answered under the id the caller asked about, and one row per person per chart: their
        // best, whichever tag it was set under.
        var best = new Dictionary<(int Player, Guid Chart), BoardScoreReading>();
        foreach (var row in rows)
        {
            var asked = owner[row.PlayerId];
            var key = (asked, row.ChartId);
            if (best.TryGetValue(key, out var held) && held.Score >= row.Score) continue;
            best[key] = new BoardScoreReading(asked, row.ChartId, row.Level, row.Score);
        }

        return new BoardScoreReadings(latest.CompletedAt.Value, best.Values.ToArray());
    }

    /// <summary>
    ///     Every board id that belongs to the same person as one of the ids asked about, mapped
    ///     back to the id that was asked about. Read off the folds the peer groups were built
    ///     from, so it agrees with them by construction rather than by re-deriving the rules —
    ///     and an id in no fold simply owns itself, which is the common case.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, int>> Owners(MixEnum mix, int snapshotId,
        IReadOnlyCollection<int> boardPlayerIds, CancellationToken cancellationToken)
    {
        var folds = new List<PreparedPeer>();
        // Both per-type ladders: the caller has no pool to give, and a peer qualified on
        // either is a peer whose rows this has to find.
        foreach (var pool in new[] { PumbilityPool.Singles, PumbilityPool.Doubles })
            folds.AddRange(await PreparedPeers(mix, pool, snapshotId, cancellationToken));

        var siblings = new Dictionary<int, int[]>();
        foreach (var fold in folds.Where(f => f.Reading.BoardPlayerIds.Count > 1))
        foreach (var id in fold.Reading.BoardPlayerIds)
            siblings[id] = fold.Reading.BoardPlayerIds.ToArray();

        var owner = new Dictionary<int, int>();
        foreach (var asked in boardPlayerIds.Distinct())
        foreach (var id in siblings.TryGetValue(asked, out var all) ? all : new[] { asked })
            owner[id] = asked;

        return owner;
    }
}
