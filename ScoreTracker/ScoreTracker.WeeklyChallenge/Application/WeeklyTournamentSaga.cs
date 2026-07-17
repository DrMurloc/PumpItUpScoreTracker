using ScoreTracker.Domain.Services;
using ScoreTracker.WeeklyChallenge.Contracts;
using ScoreTracker.WeeklyChallenge.Contracts.Events;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;
using ScoreTracker.WeeklyChallenge.Contracts.Messages;
using ScoreTracker.WeeklyChallenge.Contracts.Commands;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Application.Commands;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.WeeklyChallenge.Application
{
    internal sealed class WeeklyTournamentSaga
    (IChartRepository charts, IWeeklyTournamentRepository weeklyTournies, IPlayerStatsReader playerStats,
        ILogger<WeeklyTournamentSaga> logger, IBus bus,
        IDateTimeOffsetAccessor dateTime, IRandomNumberGenerator random) :
        IConsumer<RotateWeeklyChartsCommand>,
        IConsumer<ScoreImportCompletedEvent>,
        IRequestHandler<RegisterWeeklyChartScoreCommand>,
        IRequestHandler<GetWeeklyChartsQuery, IEnumerable<WeeklyTournamentChart>>,
        IRequestHandler<GetWeeklyChartEntriesQuery, IEnumerable<WeeklyTournamentEntry>>,
        IRequestHandler<GetPastWeeklyEntriesQuery, IEnumerable<WeeklyTournamentEntry>>,
        IRequestHandler<GetPastWeeklyDatesQuery, IEnumerable<DateTimeOffset>>,
        IRequestHandler<GetAlreadyPlayedWeeklyChartsQuery, IEnumerable<Guid>>,
        IRequestHandler<GetUserWeeklyPlacementsQuery, IEnumerable<WeeklyPlacementRecord>>
    {
        // The session-snapshot card's weekly read: current placements for whichever of
        // the batch's charts sit on this week's board.
        public async Task<IEnumerable<WeeklyPlacementRecord>> Handle(GetUserWeeklyPlacementsQuery request,
            CancellationToken cancellationToken)
        {
            var weeklyChartIds = (await weeklyTournies.GetWeeklyCharts(request.Mix, cancellationToken))
                .Select(c => c.ChartId).ToHashSet();
            var placements = new List<WeeklyPlacementRecord>();
            foreach (var chartId in request.ChartIds.Where(weeklyChartIds.Contains).Distinct())
            {
                var entries = await weeklyTournies.GetEntries(request.Mix, chartId, cancellationToken);
                var place = WeeklyChartSuggestionPolicy.ProcessIntoPlaces(entries)
                    .Where(e => e.Item2.UserId == request.UserId)
                    .Select(e => (int?)e.Item1)
                    .FirstOrDefault();
                if (place != null) placements.Add(new WeeklyPlacementRecord(chartId, place.Value));
            }

            return placements;
        }

        // Read-side pass-throughs so pages and the partner api/weeklyCharts endpoint
        // dispatch via IMediator instead of injecting the repository.
        public async Task<IEnumerable<WeeklyTournamentChart>> Handle(GetWeeklyChartsQuery request,
            CancellationToken cancellationToken)
        {
            return await weeklyTournies.GetWeeklyCharts(request.Mix, cancellationToken);
        }

        public async Task<IEnumerable<WeeklyTournamentEntry>> Handle(GetWeeklyChartEntriesQuery request,
            CancellationToken cancellationToken)
        {
            return await weeklyTournies.GetEntries(request.Mix, request.ChartId, cancellationToken);
        }

        public async Task<IEnumerable<WeeklyTournamentEntry>> Handle(GetPastWeeklyEntriesQuery request,
            CancellationToken cancellationToken)
        {
            return await weeklyTournies.GetPastEntries(request.Mix, request.Date, cancellationToken);
        }

        public async Task<IEnumerable<DateTimeOffset>> Handle(GetPastWeeklyDatesQuery request,
            CancellationToken cancellationToken)
        {
            return await weeklyTournies.GetPastDates(request.Mix, cancellationToken);
        }

        public async Task<IEnumerable<Guid>> Handle(GetAlreadyPlayedWeeklyChartsQuery request,
            CancellationToken cancellationToken)
        {
            return await weeklyTournies.GetAlreadyPlayedCharts(request.Mix, cancellationToken);
        }

        public async Task Consume(ConsumeContext<RotateWeeklyChartsCommand> context)
        {
            // Parallel boards per mix (locked decision): each rotation message rotates
            // exactly one mix's board.
            var mix = context.Message.Mix;
            var currentWeek = await weeklyTournies.GetWeeklyCharts(mix, context.CancellationToken);
            if (currentWeek.Any(w => w.ExpirationDate > dateTime.Now))
                return;

            var chartDict = (await charts.GetCharts(mix, cancellationToken: context.CancellationToken))
                .ToDictionary(c => c.Id);
            if (!chartDict.Any())
            {
                // A mix with no charts yet (Phoenix 2 before its catalog seed) has no board
                // to rotate — skip without touching histories or the (empty) board.
                logger.LogInformation("No charts exist for mix {Mix}; skipping weekly rotation", mix);
                return;
            }

            //Write User Place Histories
            var now = dateTime.Now;
            var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7;
            var nextMonday = now.AddDays(daysUntilMonday);
            // Reset at 05:00 UTC — midnight ET on the codebase's EST reference, matching the
            // update-weekly-charts cron slot. Was 03:00, but the reset users actually saw was gated
            // by the old 09:00 UTC (5am EDT) cron — the Hangfire-extraction regression this fixes.
            var nextExpiration = new DateTimeOffset(nextMonday.Year, nextMonday.Month, nextMonday.Day, 5, 0, 0, 0, 0,
                nextMonday.Offset);
            var scores = await weeklyTournies.GetEntries(mix, null, context.CancellationToken);
            foreach (var chartGroup in scores.GroupBy(s => s.ChartId))
            {
                var inRangeLeaderboard = WeeklyChartSuggestionPolicy.ProcessIntoPlaces(chartGroup).ToArray();
                await weeklyTournies.WriteHistories(mix,
                    inRangeLeaderboard.Select(e => new UserTourneyHistory(e.Item2.UserId, e.Item2.ChartId, now, e.Item1,
                        e.Item2.CompetitiveLevel,
                        e.Item2.Score, e.Item2.Plate, e.Item2.IsBroken)), context.CancellationToken);
            }

            await weeklyTournies.ClearTheBoard(mix, context.CancellationToken);

            var alreadyPlayed = (await weeklyTournies.GetAlreadyPlayedCharts(mix, context.CancellationToken))
                .Distinct()
                .ToHashSet();
            var newCharts = new HashSet<Guid>();
            var buckets = chartDict.Values
                .Where(c => c.Type == ChartType.CoOp || c.Level >= 10)
                .GroupBy(c => (c.Level, c.Type))
                .ToDictionary(g => g.Key, g => g.Select(c => c.Id).Distinct().ToHashSet());
            //Combine CoOp 4-5 into CoOp 3
            for (var players = 4; players <= 5; players++)
                MergeBucket(buckets, (players, ChartType.CoOp), (3, ChartType.CoOp));

            //Move Paradoxx into S25s
            MergeBucket(buckets, (26, ChartType.Single), (25, ChartType.Single));
            //Move 1949 and Paradoxx into D27s
            MergeBucket(buckets, (28, ChartType.Double), (27, ChartType.Double));
            //Move 1948 into D27s
            MergeBucket(buckets, (29, ChartType.Double), (27, ChartType.Double));

            foreach (var bucket in buckets)
            {
                var chartsInRange = bucket.Value.Select(c => chartDict[c]).ToArray();

                if (!chartsInRange.Any()) continue;
                var validCharts = chartsInRange.Where(r => !alreadyPlayed.Contains(r.Id)).ToArray();
                if (!validCharts.Any())
                {
                    validCharts = chartsInRange;
                    await weeklyTournies.ClearAlreadyPlayedCharts(mix, chartsInRange.Select(c => c.Id),
                        context.CancellationToken);
                }

                var nextChart = validCharts.OrderBy(r => random.Next(1000)).First();
                newCharts.Add(nextChart.Id);
                await weeklyTournies.RegisterWeeklyChart(mix, new WeeklyTournamentChart(nextChart.Id, nextExpiration),
                    context.CancellationToken);
            }

            await weeklyTournies.WriteAlreadyPlayedCharts(mix, newCharts, context.CancellationToken);

            // A real rotation happened (both early-exits above returned) — let the Discord
            // feed post the finished week and the new lineup.
            await bus.Publish(new WeeklyChartsRotatedEvent(mix), context.CancellationToken);
        }

        // A mix's catalog may lack a merged bucket entirely (Phoenix 2 launches without some
        // CoOp/boss levels) — merge only what exists rather than assuming Phoenix's shape.
        private static void MergeBucket(
            IDictionary<(SharedKernel.ValueTypes.DifficultyLevel Level, ChartType Type), HashSet<Guid>> buckets,
            (SharedKernel.ValueTypes.DifficultyLevel Level, ChartType Type) source,
            (SharedKernel.ValueTypes.DifficultyLevel Level, ChartType Type) target)
        {
            if (!buckets.TryGetValue(source, out var sourceCharts))
                return;

            if (buckets.TryGetValue(target, out var targetCharts))
                foreach (var chartId in sourceCharts)
                    targetCharts.Add(chartId);
            else
                buckets[target] = sourceCharts.ToHashSet();

            buckets.Remove(source);
        }

        // F3 (rearch C30): weekly eligibility is THIS saga's policy. The official-site
        // gateway publishes the score facts; we decide which land on the board.
        public async Task Consume(ConsumeContext<ScoreImportCompletedEvent> context)
        {
            // Entries land on the board of the mix the import ran against.
            var mix = context.Message.Mix;
            var weeklyChartIds = (await weeklyTournies.GetWeeklyCharts(mix, context.CancellationToken))
                .Select(c => c.ChartId).ToHashSet();
            foreach (var score in context.Message.Scores.Where(s => weeklyChartIds.Contains(s.ChartId)))
                await Handle(new RegisterWeeklyChartScoreCommand(
                        new WeeklyTournamentEntry(context.Message.UserId, score.ChartId, score.Score,
                            Enum.Parse<PhoenixPlate>(score.Plate), score.IsBroken, null, 10.0), mix),
                    context.CancellationToken);
        }

        public async Task Handle(RegisterWeeklyChartScoreCommand request, CancellationToken cancellationToken)
        {
            var mix = request.Mix;
            var weeklyCharts = (await weeklyTournies.GetWeeklyCharts(mix, cancellationToken))
                .Select(c => c.ChartId)
                .Distinct()
                .ToHashSet();
            if (!weeklyCharts.Contains(request.Entry.ChartId)) return;

            var chart = (await charts.GetCharts(mix, chartIds: new[] { request.Entry.ChartId },
                    cancellationToken: cancellationToken))
                .Single();
            var stats = await playerStats.GetStats(mix, request.Entry.UserId, cancellationToken);
            var competitiveLevel = chart.Type == ChartType.Single ? stats.SinglesCompetitiveLevel :
                chart.Type == ChartType.Double ? stats.DoublesCompetitiveLevel : stats.CompetitiveLevel;

            var existingEntries =
                (await weeklyTournies.GetEntries(mix, request.Entry.ChartId, cancellationToken)).ToArray();
            var existingEntry =
                existingEntries.FirstOrDefault(u =>
                    u.UserId == request.Entry.UserId);
            var existingPlace = WeeklyChartSuggestionPolicy.ProcessIntoPlaces(existingEntries)
                .Where(u => u.Item2.UserId == request.Entry.UserId)
                .Select(u => (int?)u.Item1).FirstOrDefault();

            if (existingEntry != null)
            {
                if (request.Entry.Score > existingEntry.Score)
                    existingEntry = existingEntry with { Score = request.Entry.Score };

                if (request.Entry.Plate > existingEntry.Plate)
                    existingEntry = existingEntry with { Plate = request.Entry.Plate };

                if (!request.Entry.IsBroken && existingEntry.IsBroken)
                    existingEntry = existingEntry with { IsBroken = false };

                existingEntry = existingEntry with { CompetitiveLevel = competitiveLevel };
                existingEntry = existingEntry with { PhotoUrl = request.Entry.PhotoUrl };
                await weeklyTournies.SaveEntry(mix, existingEntry, cancellationToken);
            }
            else
            {
                existingEntry = request.Entry with { CompetitiveLevel = competitiveLevel };
                await weeklyTournies.SaveEntry(mix, existingEntry, cancellationToken);
            }

            var newPlace = WeeklyChartSuggestionPolicy.ProcessIntoPlaces(existingEntries.Where(u => u.UserId != request.Entry.UserId)
                .Append(existingEntry)).First(e => e.Item2.UserId == request.Entry.UserId).Item1;
            // Placement changes drive PlayerProgress's weekly-placement milestones; the
            // per-progression Discord post retired with the hardcoded channel.
            if (existingPlace == null || existingPlace != newPlace)
                await bus.Publish(new UserWeeklyChartsProgressedEvent(request.Entry.UserId, chart.Id,
                    existingEntry.Score, existingEntry.Plate.ToString(), existingEntry.IsBroken, newPlace, mix),
                    cancellationToken);
        }
    }
}
