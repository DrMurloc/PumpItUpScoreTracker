namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     Explains every tag that left the boards between two snapshots.
///     <para>
///         A player who renames on piugame becomes two rows here: the old tag stops appearing
///         and a new one starts, carrying the same history under a name nothing connects to the
///         old one. The site regenerates the <c>#1234</c> discriminator along with the name, so
///         there is no account identifier to key on — every rename pair on record changed both
///         halves of the tag.
///     </para>
///     <para>
///         What survives a rename is the scores. A tag that vanished and a tag that appeared are
///         the same person when their board scores line up, and the test that carries the weight
///         is <b>exact</b> equality on a score that is not a perfect game: sharing a 1,000,000
///         means nothing, and sharing five identical imperfect scores across five charts does not
///         happen to strangers. Measured against every rename on record, the true match scored
///         between 11 and 201 exact matches while the best unrelated candidate scored 1.
///     </para>
///     <para>
///         Three tests, in order of authority:
///         <list type="number">
///             <item>
///                 <b>Nobody goes backwards.</b> A candidate found on one of the old tag's boards
///                 with a LOWER score is not that player — mirrored bests only ever improve. One
///                 violation disqualifies, with no threshold to argue about.
///             </item>
///             <item>
///                 <b>Scores do not evaporate.</b> If a score the old tag held would still rank
///                 comfortably inside its board today and nobody is standing there, something
///                 happened that is not a rename (a ban, usually). Never merges itself.
///             </item>
///             <item>
///                 <b>Exact matches identify the person</b> — see above.
///             </item>
///         </list>
///     </para>
///     <para>
///         The avatar is recorded and shown, never gated on: players change their name and their
///         picture in the same sitting, and requiring both to agree misses more renames than it
///         catches — of the renames this finds, well under half kept the avatar.
///     </para>
///     <para>
///         Reads the OFFICIAL reading of both snapshots and nothing else. A supplemented row is
///         rolled up from a player's own ledger on a press of an admin button, so a week where
///         the roll-up ran against a week where it did not would read as thousands of players
///         vanishing and thousands appearing (supplemented-leaderboards.md §7).
///     </para>
/// </summary>
internal static class VanishedTagAnalyzer
{
    /// <summary>Boards the old tag must have held to be worth explaining at all.</summary>
    public const int MinimumPlacements = 5;

    /// <summary>Exact non-perfect matches that make a candidate conclusive.</summary>
    public const int ExactMatchesToMerge = 5;

    /// <summary>Boards a candidate must actually stand on before it is worth an admin's time.</summary>
    public const int BoardsPresentToPropose = 3;

    /// <summary>
    ///     How far a leader must beat the runner-up to settle a two-candidate case unattended.
    ///     Five against two hundred is obvious; five against eight is not.
    /// </summary>
    public const int DominanceFactor = 5;

    /// <summary>
    ///     How far inside a board's captured depth a score must rank before its absence counts
    ///     as evidence. Boards are paged until the site stops serving rows, and the last row or
    ///     two moves between runs — every apparent disappearance on record sat within five
    ///     places of the tail. Without this margin the ban check fires on ordinary jitter and
    ///     buries the real thing.
    /// </summary>
    public const int TailMargin = 20;

    private const decimal PerfectGame = 1_000_000m;

    public static IReadOnlyList<RenameProposal> Analyze(int snapshotId,
        IReadOnlyList<PlayerDimension> players,
        IReadOnlyList<BoardDimension> boards,
        IReadOnlyList<PlacementRow> current,
        IReadOnlyList<PlacementRow> previous)
    {
        var chartBoardIds = boards.Where(b => b.LeaderboardType == LeaderboardTypes.Chart)
            .Select(b => b.Id).ToHashSet();
        var playersById = players.ToDictionary(p => p.Id);

        // Presence is judged across EVERY board, chart and rating alike: a tag still holding a
        // PUMBILITY row has not left, whatever became of its chart placements.
        var currentPlayerIds = current.Select(p => p.PlayerId).ToHashSet();
        var previousPlayerIds = previous.Select(p => p.PlayerId).ToHashSet();

        var currentChart = current.Where(p => chartBoardIds.Contains(p.LeaderboardId)).ToArray();
        var previousChart = previous.Where(p => chartBoardIds.Contains(p.LeaderboardId))
            .GroupBy(p => p.PlayerId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PlacementRow>)g.ToArray());

        // Every board's current scores, so a score the old tag held can be ranked against the
        // board as it stands now — and the row count, which is the captured depth the tail
        // margin is measured off.
        var boardScores = currentChart.GroupBy(p => p.LeaderboardId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Score).ToArray());

        // Candidates are tags on a chart board now that were on nothing at all before. Indexed
        // by board so an old tag's candidates come from the boards it actually held, rather
        // than from every tag that appeared this week.
        var appearedByBoard = currentChart
            .Where(p => !previousPlayerIds.Contains(p.PlayerId) && playersById.ContainsKey(p.PlayerId))
            .GroupBy(p => p.LeaderboardId)
            .ToDictionary(g => g.Key, g => g.Select(p => (p.PlayerId, p.Score)).ToArray());
        var appearedBoards = currentChart
            .Where(p => !previousPlayerIds.Contains(p.PlayerId))
            .GroupBy(p => p.PlayerId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.LeaderboardId).ToHashSet());

        var findings = new List<RenameProposal>();
        foreach (var (oldId, oldRows) in previousChart)
        {
            if (currentPlayerIds.Contains(oldId) || oldRows.Count < MinimumPlacements) continue;
            if (!playersById.TryGetValue(oldId, out var oldPlayer)) continue;

            var best = BestCandidate(oldRows, appearedByBoard, out var runnerUpExact);
            var suspicious = CountSuspiciousAbsences(oldRows, boardScores,
                best == null ? null : appearedBoards.GetValueOrDefault(best.Value.PlayerId));

            var candidate = best == null ? null : playersById[best.Value.PlayerId];
            var evidence = new RenameEvidence(oldRows.Count, best?.Present ?? 0, best?.ExactNonPg ?? 0,
                best?.ExactPg ?? 0, runnerUpExact, suspicious,
                candidate != null && AvatarsAgree(oldPlayer, candidate));

            findings.Add(new RenameProposal(0, oldId, candidate?.Id, oldPlayer.Username, candidate?.Username,
                Verdict(best, runnerUpExact, suspicious), evidence, ProposalStatuses.Pending, snapshotId));
        }

        return findings;
    }

    private static string Verdict(Candidate? best, int runnerUpExact, int suspiciousAbsences)
    {
        // Suspicion outranks the evidence for it. A tag whose scores should still be ranking
        // did not simply get renamed, and merging it into whoever looks closest would bury the
        // one case an admin actually needs to see.
        if (suspiciousAbsences > 0) return VanishVerdicts.Suspicious;
        if (best == null) return VanishVerdicts.DroppedOff;

        if (best.Value.ExactNonPg >= ExactMatchesToMerge)
            return best.Value.ExactNonPg >= DominanceFactor * runnerUpExact
                ? VanishVerdicts.Merge
                : VanishVerdicts.Ambiguous;

        return best.Value.Present >= BoardsPresentToPropose && best.Value.ExactNonPg >= 1
            ? VanishVerdicts.Propose
            : VanishVerdicts.DroppedOff;
    }

    /// <summary>
    ///     The strongest candidate that never contradicts the old tag, plus the exact-match
    ///     count of the next one down — which is what decides whether the leader is obviously
    ///     right or merely ahead.
    /// </summary>
    private static Candidate? BestCandidate(IReadOnlyList<PlacementRow> oldRows,
        IReadOnlyDictionary<int, (int PlayerId, decimal Score)[]> appearedByBoard, out int runnerUpExact)
    {
        var tally = new Dictionary<int, Candidate>();
        foreach (var row in oldRows)
        {
            if (!appearedByBoard.TryGetValue(row.LeaderboardId, out var onBoard)) continue;
            foreach (var (playerId, score) in onBoard)
            {
                tally.TryGetValue(playerId, out var candidate);
                candidate.PlayerId = playerId;
                candidate.Present++;
                if (score < row.Score) candidate.Regressions++;
                else if (score == row.Score && row.Score == PerfectGame) candidate.ExactPg++;
                else if (score == row.Score) candidate.ExactNonPg++;
                tally[playerId] = candidate;
            }
        }

        var ranked = tally.Values.Where(c => c.Regressions == 0)
            .OrderByDescending(c => c.ExactNonPg).ThenByDescending(c => c.Present)
            .ThenBy(c => c.PlayerId)
            .ToArray();
        runnerUpExact = ranked.Length > 1 ? ranked[1].ExactNonPg : 0;
        return ranked.Length == 0 ? null : ranked[0];
    }

    /// <summary>
    ///     Boards where a score the old tag held would still rank comfortably inside today and
    ///     the candidate is not standing there. A board that carries no rows at all this
    ///     snapshot says nothing — the sweep skips a board whose fetch failed, and that is our
    ///     gap, not the player's.
    /// </summary>
    private static int CountSuspiciousAbsences(IReadOnlyList<PlacementRow> oldRows,
        IReadOnlyDictionary<int, decimal[]> boardScores, IReadOnlySet<int>? candidateBoards)
    {
        var suspicious = 0;
        foreach (var row in oldRows)
        {
            if (candidateBoards != null && candidateBoards.Contains(row.LeaderboardId)) continue;
            if (!boardScores.TryGetValue(row.LeaderboardId, out var scores)) continue;

            var wouldRank = scores.Count(s => s > row.Score) + 1;
            if (wouldRank <= scores.Length - TailMargin) suspicious++;
        }

        return suspicious;
    }

    private static bool AvatarsAgree(PlayerDimension old, PlayerDimension candidate) =>
        old.Avatar != null && candidate.Avatar != null &&
        string.Equals(old.Avatar.ToString(), candidate.Avatar.ToString(), StringComparison.OrdinalIgnoreCase);

    private struct Candidate
    {
        public int PlayerId;
        public int Present;
        public int Regressions;
        public int ExactNonPg;
        public int ExactPg;
    }
}
