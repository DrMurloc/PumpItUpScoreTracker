using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.OfficialMirror.Domain;

internal sealed record HighlightsInput(
    MixEnum Mix,
    int SnapshotId,
    bool IsBaseline,
    IReadOnlyList<BoardDimension> Boards,
    IReadOnlyList<PlacementRow> Current,
    IReadOnlyList<PlacementRow>? Previous,
    IReadOnlyList<BoardRecordRow> BoardRecords,
    IReadOnlyList<FolderRecordRow> FolderRecords,
    CrossMixRecordHighs? CrossMix = null,
    IReadOnlySet<int>? PreviouslySeenPlayerIds = null,
    ScoringConfiguration? Scoring = null);

internal sealed record HighlightsResult(
    IReadOnlyList<HighlightRow> Highlights,
    IReadOnlyList<BoardRecordRow> UpdatedBoardRecords,
    IReadOnlyList<FolderRecordRow> UpdatedFolderRecords);

/// <summary>
///     Computes one snapshot's editorial highlights and record-book updates from the diff
///     against the previous sealed snapshot. Pure: every rule that makes the weekly board
///     lives here.
///     - Movers rank by PUMBILITY-board rank improvement (a mix without that board, i.e.
///       Phoenix, gets none).
///     - Boards-climbed counts chart boards where a player improved or newly entered.
///     - A new #1 must BEAT the all-time record; matching a standing score credits
///       nothing, while players sharing the record score in the same week co-credit.
///     - Grade firsts (every band on the ladder, PG on top, level 24+) fire once per chart
///       per band and once per folder per band; a multi-band jump claims only the highest
///       band; a folder first absorbs its chart first, and any grade first absorbs its
///       new-#1. The page leads with the highest-level first of the week.
///     - A baseline snapshot emits no highlights but still primes both record books.
/// </summary>
internal static class HighlightsCalculator
{
    public const int MoversTaken = 8;
    public const int BoardsClimbedTaken = 8;
    public const int HighlightMinimumLevel = 24;
    public const int GainersTaken = 3;
    public const string PumbilityBoardName = "PUMBILITY";
    public static readonly int[] FloorRanks = { 100, 1000 };

    public static HighlightsResult Calculate(HighlightsInput input)
    {
        var chartBoards = input.Boards
            .Where(b => b.LeaderboardType == LeaderboardTypes.Chart && b.ChartType != ChartType.CoOp.ToString())
            .ToDictionary(b => b.Id);
        var currentByBoard = input.Current.GroupBy(p => p.LeaderboardId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PlacementRow>)g.ToArray());
        var previousByBoard = (input.Previous ?? Array.Empty<PlacementRow>()).GroupBy(p => p.LeaderboardId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PlacementRow>)g.ToArray());

        var (updatedBoardRecords, updatedFolderRecords, beatenBoards) =
            UpdateRecords(input, chartBoards, currentByBoard);
        if (input.IsBaseline)
            return new HighlightsResult(Array.Empty<HighlightRow>(), updatedBoardRecords, updatedFolderRecords);

        var highlights = new List<HighlightRow>();
        highlights.AddRange(Movers(input.Boards, currentByBoard, previousByBoard));
        highlights.AddRange(BoardsClimbed(chartBoards, currentByBoard, previousByBoard, input.Previous != null));
        highlights.AddRange(RecordHighlights(input, chartBoards, previousByBoard, beatenBoards));
        highlights.AddRange(HeroSummary(input, chartBoards, currentByBoard, previousByBoard));

        return new HighlightsResult(highlights, updatedBoardRecords, updatedFolderRecords);
    }

    /// <summary>
    ///     The This Week hero's summary rows — the weekly pulse (entry and player counts),
    ///     first-ever board debuts, the top PUMBILITY value gainers, and the #100/#1000
    ///     floor marks. Diff-derived editorial like everything else: a rebuild replays them.
    /// </summary>
    private static IEnumerable<HighlightRow> HeroSummary(HighlightsInput input,
        IReadOnlyDictionary<int, BoardDimension> chartBoards,
        IReadOnlyDictionary<int, IReadOnlyList<PlacementRow>> currentByBoard,
        IReadOnlyDictionary<int, IReadOnlyList<PlacementRow>> previousByBoard)
    {
        // Debuts: on a chart board now, never on ANY board in any earlier snapshot. Every
        // debut gets a row, best place first — early weeks will be huge, deliberately, so
        // the strip can expand to the whole class. The pulse row keeps the total.
        var debuts = Array.Empty<(int PlayerId, int BestPlace)>();
        var debutTotal = 0;
        if (input.PreviouslySeenPlayerIds is { } seen)
        {
            var byPlayer = currentByBoard
                .Where(kv => chartBoards.ContainsKey(kv.Key))
                .SelectMany(kv => kv.Value)
                .Where(p => !seen.Contains(p.PlayerId))
                .GroupBy(p => p.PlayerId)
                .Select(g => (PlayerId: g.Key, BestPlace: g.Min(p => p.Place)))
                .OrderBy(d => d.BestPlace).ThenBy(d => d.PlayerId)
                .ToArray();
            debutTotal = byPlayer.Length;
            debuts = byPlayer;
        }

        for (var i = 0; i < debuts.Length; i++)
            yield return new HighlightRow(HighlightKinds.Debut, i + 1, debuts[i].PlayerId, null, null, null,
                null, null, null, debuts[i].BestPlace, null, null);

        // The pulse: new and upscored chart-board entries plus the distinct players who
        // caused them. Place-only shifts (pushed around by others) are not movement.
        if (input.Previous != null)
        {
            var newEntries = 0;
            var upscored = 0;
            var active = new HashSet<int>();
            foreach (var (boardId, placements) in currentByBoard)
            {
                if (!chartBoards.ContainsKey(boardId)) continue;
                var previousScores = previousByBoard.TryGetValue(boardId, out var prev)
                    ? prev.ToDictionary(p => p.PlayerId, p => p.Score)
                    : new Dictionary<int, decimal>();
                foreach (var placement in placements)
                    if (!previousScores.TryGetValue(placement.PlayerId, out var was))
                    {
                        newEntries++;
                        active.Add(placement.PlayerId);
                    }
                    else if (placement.Score > was)
                    {
                        upscored++;
                        active.Add(placement.PlayerId);
                    }
            }

            yield return new HighlightRow(HighlightKinds.WeeklyPulse, 1, null, null, null, null, null,
                debutTotal, null, active.Count, newEntries, upscored);
        }

        var pumbility = input.Boards.FirstOrDefault(b =>
            b.LeaderboardType == LeaderboardTypes.Rating && b.Name == PumbilityBoardName);
        if (pumbility == null || !currentByBoard.TryGetValue(pumbility.Id, out var currentBoard)) yield break;

        // Gainers: the biggest raw PUMBILITY climbs — the movers card ranks by rank-jump,
        // the hero leads with value gained.
        if (input.Previous != null && previousByBoard.TryGetValue(pumbility.Id, out var previousBoard))
        {
            var previousByPlayer = previousBoard
                .GroupBy(p => p.PlayerId)
                .ToDictionary(g => g.Key, g => g.First());
            var gainers = currentBoard
                .Where(p => previousByPlayer.TryGetValue(p.PlayerId, out var was) && p.Score > was.Score)
                .OrderByDescending(p => p.Score - previousByPlayer[p.PlayerId].Score)
                .ThenBy(p => p.Place)
                .Take(GainersTaken)
                .ToArray();
            for (var i = 0; i < gainers.Length; i++)
            {
                var was = previousByPlayer[gainers[i].PlayerId];
                yield return new HighlightRow(HighlightKinds.PumbilityGainer, i + 1, gainers[i].PlayerId,
                    null, pumbility.Id, null, null, was.Place, null, gainers[i].Score, was.Score,
                    gainers[i].Place);
            }
        }

        // Floor marks: the value holding each landmark rank and its 50x SS level
        // equivalent, both sides of the week so a held level still shows its rising floor.
        if (input.Scoring == null) yield break;
        var ordered = currentBoard.OrderBy(p => p.Place).ToArray();
        var orderedPrevious = (previousByBoard.TryGetValue(pumbility.Id, out var prevBoard)
                ? prevBoard
                : Array.Empty<PlacementRow>())
            .OrderBy(p => p.Place).ToArray();
        foreach (var rank in FloorRanks)
        {
            var value = CutlineCalculator.ValueAtRank(ordered, rank);
            if (value == null) continue;
            var prevValue = CutlineCalculator.ValueAtRank(orderedPrevious, rank);
            var level = CutlineCalculator.LevelFor(input.Scoring, ChartType.Single,
                PhoenixLetterGrade.SS, value.Value);
            var prevLevel = prevValue == null
                ? null
                : CutlineCalculator.LevelFor(input.Scoring, ChartType.Single,
                    PhoenixLetterGrade.SS, prevValue.Value);
            yield return new HighlightRow(HighlightKinds.FloorMark, rank, null, null,
                pumbility.Id, null, null, level, null, value, prevValue, prevLevel);
        }
    }

    private static (IReadOnlyList<BoardRecordRow> Boards, IReadOnlyList<FolderRecordRow> Folders,
        List<BeatenBoard> Beaten) UpdateRecords(HighlightsInput input,
            IReadOnlyDictionary<int, BoardDimension> chartBoards,
            IReadOnlyDictionary<int, IReadOnlyList<PlacementRow>> currentByBoard)
    {
        var boardRecords = input.BoardRecords.ToDictionary(r => r.LeaderboardId);
        var folderRecords = input.FolderRecords.ToDictionary(r => (r.ChartType, r.Level));
        var updatedBoards = new List<BoardRecordRow>();
        var beaten = new List<BeatenBoard>();
        var folderTouched = new HashSet<(string, int)>();

        foreach (var (boardId, placements) in currentByBoard)
        {
            if (!chartBoards.TryGetValue(boardId, out var board) || board.Level == null) continue;

            var top = (int)placements.Max(p => p.Score);
            var hadRecord = boardRecords.TryGetValue(boardId, out var record);
            var previousHigh = hadRecord ? record!.HighScore : (int?)null;
            if (previousHigh == null || top > previousHigh)
            {
                var updated = new BoardRecordRow(boardId, top, input.SnapshotId);
                boardRecords[boardId] = updated;
                updatedBoards.Add(updated);
                if (previousHigh != null)
                    beaten.Add(new BeatenBoard(board, previousHigh.Value, top));
            }

            var folderKey = (board.ChartType!, board.Level.Value);
            var folderHigh = folderRecords.TryGetValue(folderKey, out var folder) ? folder.HighScore : (int?)null;
            if (folderHigh == null || top > folderHigh)
            {
                folderRecords[folderKey] = new FolderRecordRow(folderKey.Item1, folderKey.Item2, top,
                    input.SnapshotId);
                folderTouched.Add(folderKey);
            }
        }

        var updatedFolders = folderTouched.Select(key => folderRecords[key]).ToArray();
        return (updatedBoards, updatedFolders, beaten);
    }

    private static IEnumerable<HighlightRow> Movers(IReadOnlyList<BoardDimension> boards,
        IReadOnlyDictionary<int, IReadOnlyList<PlacementRow>> currentByBoard,
        IReadOnlyDictionary<int, IReadOnlyList<PlacementRow>> previousByBoard)
    {
        var pumbility = boards.FirstOrDefault(b =>
            b.LeaderboardType == LeaderboardTypes.Rating && b.Name == PumbilityBoardName);
        if (pumbility == null) yield break;
        if (!currentByBoard.TryGetValue(pumbility.Id, out var current) ||
            !previousByBoard.TryGetValue(pumbility.Id, out var previous)) yield break;

        var previousPlaces = previous.ToDictionary(p => p.PlayerId, p => p.Place);
        var movers = current
            .Where(p => previousPlaces.ContainsKey(p.PlayerId))
            .Select(p => (Placement: p, Delta: previousPlaces[p.PlayerId] - p.Place))
            .Where(m => m.Delta > 0)
            .OrderByDescending(m => m.Delta).ThenBy(m => m.Placement.Place)
            .Take(MoversTaken)
            .ToArray();
        for (var i = 0; i < movers.Length; i++)
        {
            var (placement, _) = movers[i];
            yield return new HighlightRow(HighlightKinds.PumbilityMover, i + 1, placement.PlayerId, null,
                pumbility.Id, null, null, null, null, placement.Score,
                previousPlaces[placement.PlayerId], placement.Place);
        }
    }

    private static IEnumerable<HighlightRow> BoardsClimbed(
        IReadOnlyDictionary<int, BoardDimension> chartBoards,
        IReadOnlyDictionary<int, IReadOnlyList<PlacementRow>> currentByBoard,
        IReadOnlyDictionary<int, IReadOnlyList<PlacementRow>> previousByBoard,
        bool hasPrevious)
    {
        if (!hasPrevious) yield break;

        // A fresh entry is a climb from off the board: landing #1 on a 50-player board
        // credits 50 places, landing last credits 1. The row keeps the entered count
        // (Level) so the display can say which kind of week it was.
        var climbs = new Dictionary<int, (int Boards, int NetPlaces, int Entered)>();
        foreach (var (boardId, placements) in currentByBoard)
        {
            if (!chartBoards.ContainsKey(boardId)) continue;

            var previousPlaces = previousByBoard.TryGetValue(boardId, out var previous)
                ? previous.ToDictionary(p => p.PlayerId, p => p.Place)
                : new Dictionary<int, int>();
            foreach (var placement in placements)
            {
                var hadPlace = previousPlaces.TryGetValue(placement.PlayerId, out var previousPlace);
                if (hadPlace && previousPlace <= placement.Place) continue;

                var gained = hadPlace
                    ? previousPlace - placement.Place
                    : Math.Max(1, placements.Count - placement.Place + 1);
                var (boardCount, netPlaces, entered) = climbs.TryGetValue(placement.PlayerId, out var tally)
                    ? tally
                    : (0, 0, 0);
                climbs[placement.PlayerId] =
                    (boardCount + 1, netPlaces + gained, entered + (hadPlace ? 0 : 1));
            }
        }

        // Net places leads: with entries credited as climbs from off the board, the one
        // number carries breadth and depth together — a #250→#40 rocket on one board
        // competes with a hundred shallow climbs. Board count only breaks ties, and the
        // take is the only cap.
        var ranked = climbs
            .OrderByDescending(kv => kv.Value.NetPlaces).ThenByDescending(kv => kv.Value.Boards)
            .Take(BoardsClimbedTaken)
            .ToArray();
        for (var i = 0; i < ranked.Length; i++)
            yield return new HighlightRow(HighlightKinds.BoardsClimbed, i + 1, ranked[i].Key, null, null, null,
                null, ranked[i].Value.Entered, null, null, ranked[i].Value.NetPlaces, ranked[i].Value.Boards);
    }

    private static IEnumerable<HighlightRow> RecordHighlights(HighlightsInput input,
        IReadOnlyDictionary<int, BoardDimension> chartBoards,
        IReadOnlyDictionary<int, IReadOnlyList<PlacementRow>> previousByBoard,
        List<BeatenBoard> beatenBoards)
    {
        // Folder firsts resolve before chart firsts so a folder banner can absorb the
        // chart-level row for the same achievement.
        var eligible = beatenBoards
            .Where(b => b.Board.Level >= HighlightMinimumLevel)
            .ToArray();
        var priorFolderHighs = input.FolderRecords.ToDictionary(r => (r.ChartType, r.Level), r => r.HighScore);
        var crossMix = input.CrossMix ?? CrossMixRecordHighs.Empty;

        var folderFirstBoards = new HashSet<int>();
        var folderRows = new List<HighlightRow>();
        foreach (var folderGroup in eligible.GroupBy(b => (b.Board.ChartType!, b.Board.Level!.Value)))
        {
            // "First ever" spans every mix: the folder's prior band is the best of this
            // mix's record book and the other mixes' (each banded in its own table) — a
            // Phoenix-era AA makes a Phoenix 2 re-clear a reclear, not a first.
            var priorRank = priorFolderHighs.TryGetValue(folderGroup.Key, out var high)
                ? GradeBandLadder.Of(high, input.Mix).Rank
                : (int?)null;
            if (crossMix.FolderBandRanks.TryGetValue(folderGroup.Key, out var crossFolderRank))
                priorRank = Math.Max(priorRank ?? 0, crossFolderRank);
            if (priorRank == null) continue;

            var folderTop = folderGroup.Max(b => b.NewHigh);
            var (newRank, newBand) = GradeBandLadder.Of(folderTop, input.Mix);
            // A board record can fall while the folder high stands above it — the folder
            // first only fires when the folder's own band moved UP.
            if (newRank <= priorRank) continue;

            foreach (var beaten in folderGroup.Where(b => b.NewHigh == folderTop))
            {
                folderFirstBoards.Add(beaten.Board.Id);
                folderRows.AddRange(ClaimantRows(input, beaten, HighlightKinds.FolderGradeFirst, newBand,
                    previousByBoard));
            }
        }

        var chartRows = new List<HighlightRow>();
        var numberOneRows = new List<HighlightRow>();
        foreach (var beaten in eligible.Where(b => !folderFirstBoards.Contains(b.Board.Id)))
        {
            // Same cross-mix rule per chart: a band already claimed on this chart in any
            // mix cannot be a world first again — but beating this mix's standing record
            // still counts below as a new #1.
            var priorRank = GradeBandLadder.Of(beaten.PreviousHigh, input.Mix).Rank;
            if (beaten.Board.ChartId is { } chartId &&
                crossMix.ChartBandRanks.TryGetValue(chartId, out var crossChartRank))
                priorRank = Math.Max(priorRank, crossChartRank);
            var (newRank, newBand) = GradeBandLadder.Of(beaten.NewHigh, input.Mix);
            if (newRank > priorRank)
                chartRows.AddRange(ClaimantRows(input, beaten, HighlightKinds.ChartGradeFirst, newBand,
                    previousByBoard));
            // A perfect that isn't a first (the chart's PG lives on another mix) tells no
            // dethroning story either — tying the ceiling never lists as a new #1.
            else if (beaten.NewHigh < 1_000_000)
                numberOneRows.AddRange(ClaimantRows(input, beaten, HighlightKinds.NewNumberOne, null,
                    previousByBoard));
        }

        return Order(folderRows).Concat(Order(chartRows)).Concat(Order(numberOneRows));

        static IEnumerable<HighlightRow> Order(List<HighlightRow> rows)
        {
            return rows.OrderByDescending(r => GradeBandLadder.RankOf(r.GradeBand))
                .ThenByDescending(r => r.Score)
                .Select((r, i) => r with { SortOrder = i + 1 });
        }
    }

    private static IEnumerable<HighlightRow> ClaimantRows(HighlightsInput input, BeatenBoard beaten, string kind,
        string? band, IReadOnlyDictionary<int, IReadOnlyList<PlacementRow>> previousByBoard)
    {
        var current = input.Current.Where(p => p.LeaderboardId == beaten.Board.Id).ToArray();
        var dethroned = previousByBoard.TryGetValue(beaten.Board.Id, out var previous)
            ? previous.Where(p => p.Place == 1).OrderBy(p => p.PlayerId).Select(p => (int?)p.PlayerId)
                .FirstOrDefault()
            : null;
        // Same-week rule: everyone sharing the new record score co-credits; later ties
        // never re-fire because the record book already holds the score.
        return current.Where(p => (int)p.Score == beaten.NewHigh)
            .Select(p => new HighlightRow(kind, 0, p.PlayerId,
                dethroned == p.PlayerId ? null : dethroned, beaten.Board.Id, beaten.Board.ChartId,
                beaten.Board.ChartType, beaten.Board.Level, band, beaten.NewHigh, beaten.PreviousHigh, null));
    }

    private sealed record BeatenBoard(BoardDimension Board, int PreviousHigh, int NewHigh);
}
