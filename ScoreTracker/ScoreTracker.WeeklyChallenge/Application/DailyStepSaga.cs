using ScoreTracker.WeeklyChallenge.Contracts;
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

namespace ScoreTracker.WeeklyChallenge.Application;

// Daily Step: the site-run daily challenge board (sibling of the weekly tournament, same bounded
// context). Rotates once a day at midnight ET, registers imported scores that land on the live
// chart, and flips to "lowest passing wins" on the deterministic weekly Limbo day.
internal sealed class DailyStepSaga(
    IDailyStepRepository dailySteps,
    IChartRepository charts,
    IPlayerStatsReader playerStats,
    IDateTimeOffsetAccessor dateTime,
    IRandomNumberGenerator random,
    ILogger<DailyStepSaga> logger) :
    IConsumer<RotateDailyStepCommand>,
    IConsumer<DailyStepScoreObservedEvent>,
    IRequestHandler<GetDailyStepQuery, DailyStepBoard?>,
    IRequestHandler<GetDailyStepEntriesQuery, IEnumerable<WeeklyTournamentEntry>>,
    IRequestHandler<GetDailyStepPlacementQuery, DailyStepPlacement?>
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
        var entries = (await dailySteps.GetEntries(mix, finishing.ChartId, ct)).ToArray();
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

        var chart = (await charts.GetCharts(msg.Mix, chartIds: new[] { msg.ChartId }, cancellationToken: ct))
            .SingleOrDefault();
        if (chart == null) return;
        var stats = await playerStats.GetStats(msg.Mix, msg.UserId, ct);
        var competitiveLevel = chart.Type == ChartType.Single ? stats.SinglesCompetitiveLevel
            : chart.Type == ChartType.Double ? stats.DoublesCompetitiveLevel
            : stats.CompetitiveLevel;

        var existing = (await dailySteps.GetEntries(msg.Mix, msg.ChartId, ct))
            .FirstOrDefault(e => e.UserId == msg.UserId);
        // Keep the board-appropriate extreme: lowest passing on Limbo, highest otherwise.
        if (existing != null && !(board.IsLimbo ? score < (int)existing.Score : score > (int)existing.Score))
            return;

        await dailySteps.SaveEntry(msg.Mix,
            new WeeklyTournamentEntry(msg.UserId, msg.ChartId, score, plate, isBroken, null, competitiveLevel),
            ct);
    }

    public async Task<DailyStepBoard?> Handle(GetDailyStepQuery request, CancellationToken cancellationToken)
    {
        return await dailySteps.GetCurrentChart(request.Mix, cancellationToken);
    }

    public async Task<IEnumerable<WeeklyTournamentEntry>> Handle(GetDailyStepEntriesQuery request,
        CancellationToken cancellationToken)
    {
        return await dailySteps.GetEntries(request.Mix, null, cancellationToken);
    }

    public async Task<DailyStepPlacement?> Handle(GetDailyStepPlacementQuery request,
        CancellationToken cancellationToken)
    {
        var board = await dailySteps.GetCurrentChart(request.Mix, cancellationToken);
        if (board == null) return null;
        var entries = (await dailySteps.GetEntries(request.Mix, board.ChartId, cancellationToken)).ToArray();
        var ranked = (board.IsLimbo
            ? WeeklyChartSuggestionPolicy.ProcessIntoPlacesAscending(entries)
            : WeeklyChartSuggestionPolicy.ProcessIntoPlaces(entries)).ToArray();
        var mine = ranked.Where(r => r.Item2.UserId == request.UserId).Select(r => (int?)r.Item1)
            .FirstOrDefault();
        return mine == null ? null : new DailyStepPlacement(mine.Value, ranked.Length, board.IsLimbo);
    }

    // The board resets at 05:00 UTC — midnight ET on the codebase's EST reference (the
    // rotate-daily-step cron slot). ExpirationDate is that exact moment so the widget countdown is
    // honest. Rotation fires at ~05:00 UTC, so the next reset is the following day.
    private static DateTimeOffset NextResetAfter(DateTimeOffset now)
    {
        var todayReset = new DateTimeOffset(now.Year, now.Month, now.Day, 5, 0, 0, TimeSpan.Zero);
        return now < todayReset ? todayReset : todayReset.AddDays(1);
    }
}
