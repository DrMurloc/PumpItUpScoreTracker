namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     Folds a board's official rows together with the ledger bests of linked public players.
///     Pure, and the only place the merge rules live — the roll-up uses it to decide what to
///     store, and the supplemented read uses it to rank what it found, so the two cannot
///     disagree about what the board looks like.
///     <para>
///         **Supplement fills gaps; it never refreshes.** A player the official board already
///         lists is left exactly as piugame published them, even when their own ledger is newer
///         and better. Storing the upgrade meant two rows for one human on one board, and the
///         placement key is (Snapshot, Leaderboard, Place, Player) — so an improvement too
///         small to move them past the player above collides with their own official row. It
///         is also the honest reading: every other row on that board is the seal's data, and
///         one player's fresher score among them is a different week's answer.
///     </para>
///     <para>
///         On a complete chart board supplemented rows can only land below the official tail —
///         a player absent from the top 300 has, by definition, a score below the 300th. That
///         is a property of the data rather than a rule to impose, so nothing here enforces
///         it; <see cref="RowsAboveOfficialTail" /> measures it instead, and a non-zero count
///         means the official board was short (a skipped or truncated board), which is worth
///         saying out loud rather than hiding.
///     </para>
/// </summary>
internal static class SupplementMerge
{
    /// <summary>
    ///     The supplemented rows to store for one board: the ledger bests of players the board
    ///     does not already list, each carrying its merged place. A player with an official row
    ///     contributes nothing, which is what guarantees no stored supplemented row can share a
    ///     (Place, Player) with an official one — the placement key's whole safety.
    /// </summary>
    public static IReadOnlyList<PlacementRow> RowsToStore(int leaderboardId,
        IReadOnlyList<PlacementRow> official, IReadOnlyList<(int PlayerId, decimal Score)> ledger)
    {
        var listed = official.Select(r => r.PlayerId).ToHashSet();

        var candidates = ledger
            .Where(l => !listed.Contains(l.PlayerId))
            .Select(l => new PlacementRow(leaderboardId, l.PlayerId, 0, l.Score, true));

        return MergedBoard(official.Concat(candidates))
            .Where(r => r.IsSupplemented)
            .ToArray();
    }

    /// <summary>The board as the supplemented view shows it: one row per player, re-ranked.</summary>
    public static IReadOnlyList<PlacementRow> MergedBoard(IEnumerable<PlacementRow> rows) =>
        Rank(Collapse(rows));

    /// <summary>
    ///     Many boards' rows at once, each merged within itself. Ranking is per board, so a
    ///     whole snapshot has to be split before it is ranked rather than after — the highlights
    ///     pass reads a snapshot this way.
    /// </summary>
    public static IReadOnlyList<PlacementRow> MergedBoards(IEnumerable<PlacementRow> rows) =>
        rows.GroupBy(r => r.LeaderboardId)
            .SelectMany(MergedBoard)
            .ToArray();

    /// <summary>
    ///     The same merge over the hub's joined read shape. Two concrete methods rather than
    ///     one generic over five accessors — the parallel between them is obvious to read, and
    ///     the generic was not.
    /// </summary>
    public static IReadOnlyList<PlacementDetail> MergedBoards(IEnumerable<PlacementDetail> rows) =>
        rows.GroupBy(r => r.LeaderboardId)
            .SelectMany(board => Placements.Olympic(
                    board.GroupBy(r => r.PlayerId)
                        .Select(g => g.OrderByDescending(r => r.Score).ThenBy(r => r.IsSupplemented).First())
                        .OrderBy(r => r.IsSupplemented).ThenBy(r => r.PlayerId),
                    r => r.Score)
                .Select(x => x.Item with { Place = x.Place }))
            .ToArray();

    /// <summary>
    ///     One row per player. A supplemented row only exists where it beat the player's
    ///     official row, so taking the higher score is the same rule read back — but it is
    ///     stated rather than assumed, because the stored rows outlive the run that wrote them
    ///     and an official score can be re-scraped upward on a later sweep.
    /// </summary>
    public static IReadOnlyList<PlacementRow> Collapse(IEnumerable<PlacementRow> rows) =>
        rows.GroupBy(r => r.PlayerId)
            .Select(g => g.OrderByDescending(r => r.Score).ThenBy(r => r.IsSupplemented).First())
            .ToArray();

    /// <summary>
    ///     Ranks a collapsed board with the sweep's own Olympic tie rule. Ties order official
    ///     ahead of supplemented and then by player id — arbitrary, but fixed, so a paginated
    ///     board does not reshuffle between one render and the next.
    /// </summary>
    public static IReadOnlyList<PlacementRow> Rank(IEnumerable<PlacementRow> collapsed) =>
        Placements.Olympic(
                collapsed.OrderBy(r => r.IsSupplemented).ThenBy(r => r.PlayerId),
                r => r.Score)
            .Select(x => x.Item with { Place = x.Place })
            .ToArray();

    /// <summary>
    ///     How many supplemented rows placed above the official board's last row. Zero on a
    ///     complete chart board; anything else says the official side was short this week.
    /// </summary>
    public static int RowsAboveOfficialTail(IReadOnlyList<PlacementRow> merged)
    {
        var lastOfficial = merged.Where(r => !r.IsSupplemented).Select(r => (int?)r.Place).Max();
        return lastOfficial == null
            ? merged.Count(r => r.IsSupplemented)
            : merged.Count(r => r.IsSupplemented && r.Place < lastOfficial);
    }
}
