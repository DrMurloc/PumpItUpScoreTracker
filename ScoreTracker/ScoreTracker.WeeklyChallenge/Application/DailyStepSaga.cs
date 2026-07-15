using ScoreTracker.WeeklyChallenge.Contracts;
using ScoreTracker.WeeklyChallenge.Contracts.Commands;
using ScoreTracker.WeeklyChallenge.Contracts.Messages;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;
using ScoreTracker.WeeklyChallenge.Domain;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.WeeklyChallenge.Application;

// Daily Step: the site-run daily challenge board (sibling of the weekly tournament, same bounded
// context). Rotates once a day at midnight ET, registers imported scores that land on the live
// chart (marked Official), accepts manual widget submissions (marked Manual — the only path to a
// deliberate Limbo low-pass), and flips to "lowest passing wins" on the deterministic weekly Limbo day.
internal sealed class DailyStepSaga(
    IDailyStepRepository dailySteps,
    IChartRepository charts,
    IPlayerStatsReader playerStats,
    ICurrentUserAccessor currentUser,
    IDateTimeOffsetAccessor dateTime,
    IRandomNumberGenerator random,
    IUserReader users,
    ILogger<DailyStepSaga> logger) :
    IConsumer<RotateDailyStepCommand>,
    IConsumer<DailyStepScoreObservedEvent>,
    IRequestHandler<RecordDailyStepScoreCommand>,
    IRequestHandler<GetDailyStepQuery, DailyStepBoard?>,
    IRequestHandler<GetDailyStepEntriesQuery, IEnumerable<DailyStepEntry>>,
    IRequestHandler<GetDailyStepPlacementQuery, DailyStepPlacement?>,
    IRequestHandler<GetDailyStepBoardQuery, DailyStepBoardView?>,
    IRequestHandler<GetUserDailyStepHistoryQuery, IEnumerable<DailyStepHistoryRecord>>
{
    // Normal days draw a challenge-worthy chart; Limbo days drop to something almost anyone can
    // pass (levels 1–15, owner). Singles/doubles only — a shared solo daily, no co-op.
    private const int NormalMinLevel = 16;
    private const int NormalMaxLevel = 24;
    private const int LimboMinLevel = 1;
    private const int LimboMaxLevel = 15;

    public async Task Consume(ConsumeContext<RotateDailyStepCommand> context)
    {
        var mix = context.Message.Mix;
        var ct = context.CancellationToken;
        var now = dateTime.Now;

        var current = await dailySteps.GetCurrentChart(mix, ct);
        if (current != null && current.ExpirationDate > now)
            return; // today's board is still live — idempotent no-op (the daily cron is a retry envelope)

        var chartDict = (await charts.GetCharts(mix, cancellationToken: ct)).ToDictionary(c => c.Id);
        if (chartDict.Count == 0)
        {
            // A mix without a chart catalog yet (Phoenix 2 pre-seed) has no board to draw.
            logger.LogInformation("No charts exist for mix {Mix}; skipping daily step rotation", mix);
            return;
        }

        if (current != null) await SnapshotFinishingBoard(mix, current, ct);
        await dailySteps.ClearBoard(mix, ct);

        var isLimbo = DailyStepLimboPolicy.IsLimboDay(now);
        var (minLevel, maxLevel) = isLimbo
            ? (LimboMinLevel, LimboMaxLevel)
            : (NormalMinLevel, NormalMaxLevel);
        var candidates = chartDict.Values
            .Where(c => c.Type is ChartType.Single or ChartType.Double
                        && (int)c.Level >= minLevel && (int)c.Level <= maxLevel)
            .ToArray();
        if (candidates.Length == 0)
        {
            logger.LogInformation(
                "No {Band} charts (levels {Min}-{Max}) for mix {Mix}; skipping daily step rotation",
                isLimbo ? "Limbo" : "standard", minLevel, maxLevel, mix);
            return;
        }

        // Dupes across days are acceptable (owner: no already-played tracking), so a plain draw.
        var chosen = candidates[random.Next(candidates.Length)];
        await dailySteps.RegisterDailyChart(mix,
            new DailyStepBoard(chosen.Id, now, isLimbo, NextResetAfter(now)), ct);
    }

    private async Task SnapshotFinishingBoard(MixEnum mix, DailyStepBoard finishing, CancellationToken ct)
    {
        var entries = (await dailySteps.GetEntries(mix, finishing.ChartId, ct)).Select(ToRanked).ToArray();
        if (entries.Length == 0) return;
        var ranked = finishing.IsLimbo
            ? WeeklyChartSuggestionPolicy.ProcessIntoPlacesAscending(entries)
            : WeeklyChartSuggestionPolicy.ProcessIntoPlaces(entries);
        var placings = ranked
            .Select(r => new DailyStepPlacing(r.Item2.UserId, r.Item2.ChartId, finishing.ForDate,
                finishing.IsLimbo, r.Item1, r.Item2.Score, r.Item2.Plate, r.Item2.IsBroken,
                r.Item2.CompetitiveLevel))
            .ToArray();
        if (placings.Length > 0) await dailySteps.WriteHistories(mix, placings, ct);
    }

    public async Task Consume(ConsumeContext<DailyStepScoreObservedEvent> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var board = await dailySteps.GetCurrentChart(msg.Mix, ct);
        if (board == null || board.ChartId != msg.ChartId)
            return; // the play doesn't match today's live chart (e.g. the board rotated mid-import)

        int score;
        PhoenixPlate plate;
        bool isBroken;
        if (board.IsLimbo)
        {
            // Limbo needs a passing run; a broken "best" doesn't qualify.
            if (msg.LowestPassScore == null) return;
            score = msg.LowestPassScore.Value;
            plate = Enum.Parse<PhoenixPlate>(msg.LowestPassPlate!);
            isBroken = false;
        }
        else
        {
            score = msg.BestScore;
            plate = Enum.Parse<PhoenixPlate>(msg.BestPlate);
            isBroken = msg.BestIsBroken;
        }

        await UpsertEntry(msg.Mix, board, msg.UserId, score, plate, isBroken, ChallengeEntrySource.Official, ct);
    }

    // A manual submission from the widget's Record popover: daily-board-only (never the ledger, so a
    // deliberate Limbo low never pollutes a PB), stamped Manual. The caller is resolved server-side
    // and the score always counts as a pass — a selected plate implies completion.
    public async Task Handle(RecordDailyStepScoreCommand request, CancellationToken cancellationToken)
    {
        var board = await dailySteps.GetCurrentChart(request.Mix, cancellationToken);
        if (board == null) return;
        await UpsertEntry(request.Mix, board, currentUser.User.Id, request.Score, request.Plate, false,
            ChallengeEntrySource.Manual, cancellationToken);
    }

    // The single intake seam for both sources: keep the board-appropriate extreme — lowest passing on
    // Limbo, highest otherwise — and stamp the winning score's source. So a manual Limbo low naturally
    // beats an official best on a Limbo day (and reads Manual), while a higher official best wins a
    // normal day (and reads Official).
    private async Task UpsertEntry(MixEnum mix, DailyStepBoard board, Guid userId, PhoenixScore score,
        PhoenixPlate plate, bool isBroken, ChallengeEntrySource source, CancellationToken ct)
    {
        var chart = (await charts.GetCharts(mix, chartIds: new[] { board.ChartId }, cancellationToken: ct))
            .SingleOrDefault();
        if (chart == null) return;
        var stats = await playerStats.GetStats(mix, userId, ct);
        var competitiveLevel = chart.Type == ChartType.Single ? stats.SinglesCompetitiveLevel
            : chart.Type == ChartType.Double ? stats.DoublesCompetitiveLevel
            : stats.CompetitiveLevel;

        var existing = (await dailySteps.GetEntries(mix, board.ChartId, ct))
            .FirstOrDefault(e => e.UserId == userId);
        if (existing != null &&
            !(board.IsLimbo ? (int)score < (int)existing.Score : (int)score > (int)existing.Score))
            return;

        await dailySteps.SaveEntry(mix,
            new DailyStepEntry(userId, board.ChartId, score, plate, isBroken, competitiveLevel, source), ct);
    }

    public async Task<DailyStepBoard?> Handle(GetDailyStepQuery request, CancellationToken cancellationToken)
    {
        return await dailySteps.GetCurrentChart(request.Mix, cancellationToken);
    }

    public async Task<IEnumerable<DailyStepEntry>> Handle(GetDailyStepEntriesQuery request,
        CancellationToken cancellationToken)
    {
        return await dailySteps.GetEntries(request.Mix, null, cancellationToken);
    }

    public async Task<DailyStepPlacement?> Handle(GetDailyStepPlacementQuery request,
        CancellationToken cancellationToken)
    {
        var board = await dailySteps.GetCurrentChart(request.Mix, cancellationToken);
        if (board == null) return null;
        var entries = (await dailySteps.GetEntries(request.Mix, board.ChartId, cancellationToken))
            .Select(ToRanked).ToArray();
        var ranked = (board.IsLimbo
            ? WeeklyChartSuggestionPolicy.ProcessIntoPlacesAscending(entries)
            : WeeklyChartSuggestionPolicy.ProcessIntoPlaces(entries)).ToArray();
        var mine = ranked.Where(r => r.Item2.UserId == request.UserId).Select(r => (int?)r.Item1)
            .FirstOrDefault();
        return mine == null ? null : new DailyStepPlacement(mine.Value, ranked.Length, board.IsLimbo);
    }

    // The challenges page's board read: ranked, display-enriched rows in one dispatch. Ranking is
    // the same policy the rotation snapshots and the placement query report — one truth for places.
    public async Task<DailyStepBoardView?> Handle(GetDailyStepBoardQuery request,
        CancellationToken cancellationToken)
    {
        var board = await dailySteps.GetCurrentChart(request.Mix, cancellationToken);
        if (board == null) return null;
        var entries = (await dailySteps.GetEntries(request.Mix, board.ChartId, cancellationToken)).ToArray();
        // Entries are unique per user on one chart, so ranked rows map back to their Source-bearing
        // originals by user id.
        var byUser = entries.ToDictionary(e => e.UserId);
        var ranked = (board.IsLimbo
            ? WeeklyChartSuggestionPolicy.ProcessIntoPlacesAscending(entries.Select(ToRanked))
            : WeeklyChartSuggestionPolicy.ProcessIntoPlaces(entries.Select(ToRanked))).ToArray();
        var userDict = (await users.GetUsers(ranked.Select(r => r.Item2.UserId).Distinct().ToArray(),
            cancellationToken)).ToDictionary(u => u.Id);
        var rows = ranked
            .Select(r => new DailyStepBoardRow(r.Item1, userDict.GetValueOrDefault(r.Item2.UserId),
                byUser[r.Item2.UserId]))
            .ToArray();
        var myRow = request.UserId == null ? null : rows.FirstOrDefault(r => r.Entry.UserId == request.UserId);
        return new DailyStepBoardView(board, rows, myRow);
    }

    public async Task<IEnumerable<DailyStepHistoryRecord>> Handle(GetUserDailyStepHistoryQuery request,
        CancellationToken cancellationToken)
    {
        return await dailySteps.GetUserHistory(request.Mix, request.UserId, request.Take, cancellationToken);
    }

    // The placement policy (shared with Weekly) ranks WeeklyTournamentEntry; Daily entries carry an
    // extra Source that ordering ignores, so map across for the ranking only.
    private static WeeklyTournamentEntry ToRanked(DailyStepEntry e) =>
        new(e.UserId, e.ChartId, e.Score, e.Plate, e.IsBroken, null, e.CompetitiveLevel);

    // The board resets at 05:00 UTC — midnight ET on the codebase's EST reference (the
    // rotate-daily-step cron slot). ExpirationDate is that exact moment so the widget countdown is
    // honest. Rotation fires at ~05:00 UTC, so the next reset is the following day.
    private static DateTimeOffset NextResetAfter(DateTimeOffset now)
    {
        var todayReset = new DateTimeOffset(now.Year, now.Month, now.Day, 5, 0, 0, TimeSpan.Zero);
        return now < todayReset ? todayReset : todayReset.AddDays(1);
    }
}
