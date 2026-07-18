namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     Detects likely player renames between two snapshots: a tag with real board presence
///     vanished this week while a new tag appeared carrying the same avatar and
///     substantially the same top-50 chart placements (same board, score at least the old
///     one — mirrored scores only ever improve). Emits proposals only; an admin accepts or
///     dismisses each. An old tag matching several new tags equally well stays unproposed —
///     ambiguity is churn, not evidence.
/// </summary>
internal static class RenameProposalDetector
{
    public const int MinimumPlacements = 10;
    public const double MinimumOverlap = 0.7;
    private const int TopPlacementsConsidered = 50;

    public static IReadOnlyList<RenameProposal> Detect(int snapshotId,
        IReadOnlyList<PlayerDimension> players,
        IReadOnlyList<BoardDimension> boards,
        IReadOnlyList<PlacementRow> current,
        IReadOnlyList<PlacementRow> previous)
    {
        var chartBoardIds = boards.Where(b => b.LeaderboardType == LeaderboardTypes.Chart)
            .Select(b => b.Id).ToHashSet();
        var playersById = players.ToDictionary(p => p.Id);
        var currentPlayerIds = current.Select(p => p.PlayerId).ToHashSet();
        var previousPlayerIds = previous.Select(p => p.PlayerId).ToHashSet();

        var currentChartRows = current.Where(p => chartBoardIds.Contains(p.LeaderboardId))
            .GroupBy(p => p.PlayerId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(p => p.LeaderboardId, p => p.Score));
        var previousChartRows = previous.Where(p => chartBoardIds.Contains(p.LeaderboardId))
            .GroupBy(p => p.PlayerId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PlacementRow>)g.ToArray());

        var appeared = currentChartRows.Keys
            .Where(id => !previousPlayerIds.Contains(id) && playersById.ContainsKey(id))
            .ToArray();
        if (appeared.Length == 0) return Array.Empty<RenameProposal>();

        var proposals = new List<RenameProposal>();
        foreach (var (oldId, oldRows) in previousChartRows)
        {
            if (currentPlayerIds.Contains(oldId) || oldRows.Count < MinimumPlacements) continue;
            if (!playersById.TryGetValue(oldId, out var oldPlayer) || oldPlayer.Avatar == null) continue;

            var oldTop = oldRows.OrderByDescending(r => r.Score).Take(TopPlacementsConsidered).ToArray();
            var qualifying = new List<(PlayerDimension Player, int Overlap)>();
            foreach (var newId in appeared)
            {
                var newPlayer = playersById[newId];
                if (newPlayer.Avatar == null ||
                    !string.Equals(newPlayer.Avatar.ToString(), oldPlayer.Avatar.ToString(),
                        StringComparison.OrdinalIgnoreCase)) continue;

                var newRows = currentChartRows[newId];
                var overlap = oldTop.Count(r =>
                    newRows.TryGetValue(r.LeaderboardId, out var score) && score >= r.Score);
                if (overlap / (double)oldTop.Length >= MinimumOverlap)
                    qualifying.Add((newPlayer, overlap));
            }

            var best = qualifying.OrderByDescending(q => q.Overlap).Take(2).ToArray();
            if (best.Length == 0 || (best.Length > 1 && best[0].Overlap == best[1].Overlap)) continue;

            proposals.Add(new RenameProposal(0, oldId, best[0].Player.Id, oldPlayer.Username,
                best[0].Player.Username, true, best[0].Overlap, ProposalStatuses.Pending, snapshotId));
        }

        return proposals;
    }
}
