using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
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
    private readonly IOfficialSnapshotRepository _snapshots;
    private readonly IUserReader _users;

    public BoardPeerReader(IOfficialSnapshotRepository snapshots, IUserReader users)
    {
        _snapshots = snapshots;
        _users = users;
    }

    /// <summary>
    ///     The per-type PUMBILITY board a chart type is priced against. Anything but Singles or
    ///     Doubles reads the combined board — a co-op chart has no pool of its own, and the
    ///     combined board is the only honest stand-in.
    /// </summary>
    public static string BoardNameFor(ChartType chartType)
    {
        return chartType switch
        {
            ChartType.Single => PumbilityBoards.Singles,
            ChartType.Double => PumbilityBoards.Doubles,
            _ => PumbilityBoards.Combined
        };
    }

    /// <summary>A tag as both spellings write it: the site's NAME #1234 and piugame's NAME#1234.</summary>
    private static string Normalise(string tag)
    {
        return tag.Replace(" ", string.Empty);
    }

    public async Task<BoardPeerGroupReading?> GetBoardPeers(MixEnum mix, ChartType chartType, double minimumPool,
        double maximumPool, CancellationToken cancellationToken)
    {
        if (minimumPool > maximumPool) return null;

        var latest = await _snapshots.GetLatestSealed(mix, cancellationToken);
        if (latest?.CompletedAt == null) return null;
        var asOf = latest.CompletedAt.Value;

        var boardName = BoardNameFor(chartType);
        var board = (await _snapshots.GetBoards(mix, cancellationToken))
            .FirstOrDefault(b => b.LeaderboardType == LeaderboardTypes.Rating && b.Name == boardName);
        // Phoenix publishes one PUMBILITY board and no per-type split, so asking it for Singles is a
        // legitimate miss rather than a failure — the caller reads "no board peers on this mix".
        if (board == null) return new BoardPeerGroupReading(asOf, Array.Empty<BoardPeerReading>());

        // Official rows only: a supplemented row is our own arithmetic laid over the board, and a
        // peer's pool has to be the number piugame published or the window is measuring two things.
        var rows = (await _snapshots.GetBoardPlacements(latest.Id, board.Id, PlacementScope.OfficialOnly,
                cancellationToken))
            .Where(p => (double)p.Score >= minimumPool && (double)p.Score <= maximumPool)
            .ToArray();
        if (rows.Length == 0) return new BoardPeerGroupReading(asOf, Array.Empty<BoardPeerReading>());

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
                return new BoardPeerReading(
                    g.OrderByDescending(c => c.Row.Score).Select(c => c.Player.Id).ToArray(),
                    best.Player.Username,
                    (double)best.Row.Score,
                    // A private account is a board player: named by its public tag, scored from
                    // public rows, and never reported as the account it happens to be (D61).
                    account is { IsPublic: true } ? account.Id : null);
            })
            .ToArray();

        return new BoardPeerGroupReading(asOf, peers);
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

        var rows = await _snapshots.GetChartHistoryFor(mix, boardPlayerIds, chartType, minimumLevel, maximumLevel,
            PlacementScope.OfficialOnly, cancellationToken);

        return rows.Select(r => new BoardScoreReading(r.PlayerId, r.ChartId, r.Level, r.Score)).ToArray();
    }
}
