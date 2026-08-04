namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     Folds a board's official rows together with the ledger bests of linked public players.
///     Pure, and the only place the merge rules live — the roll-up uses it to decide what to
///     store, and the supplemented read uses it to rank what it found, so the two cannot
///     disagree about what the board looks like.
///     <para>
///         One human, one row: a player who is on the official board and also has a ledger
///         best keeps whichever score is higher, because scores only improve. That is the only
///         reason a supplemented row is ever stored for someone already on the board.
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
    ///     The supplemented rows to store for one board: the ledger bests that earn a place,
    ///     each carrying its merged place. Ledger entries that lose to the player's own
    ///     official row are dropped rather than stored and filtered later.
    /// </summary>
    public static IReadOnlyList<PlacementRow> RowsToStore(int leaderboardId,
        IReadOnlyList<PlacementRow> official, IReadOnlyList<(int PlayerId, decimal Score)> ledger)
    {
        var officialByPlayer = official
            .GroupBy(r => r.PlayerId)
            .ToDictionary(g => g.Key, g => g.Max(r => r.Score));

        var candidates = ledger
            .Where(l => !officialByPlayer.TryGetValue(l.PlayerId, out var theirs) || l.Score > theirs)
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
