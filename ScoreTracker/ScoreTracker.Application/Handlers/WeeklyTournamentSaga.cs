using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class WeeklyTournamentSaga
    (IChartRepository charts, IWeeklyTournamentRepository weeklyTournies, IPlayerStatsRepository playerStats,
        IBotClient bot,
        ILogger<WeeklyTournamentSaga> logger, IUserRepository users) :
        IConsumer<UpdateWeeklyChartsEvent>,
        IRequestHandler<RegisterWeeklyChartScore>
    {
        public async Task Consume(ConsumeContext<UpdateWeeklyChartsEvent> context)
        {
            var currentWeek = await weeklyTournies.GetWeeklyCharts(context.CancellationToken);
            if (currentWeek.Any(w => w.ExpirationDate > DateTimeOffset.Now))
                return;
            //Write User Place Histories
            var now = DateTimeOffset.Now;
            var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
            var nextMonday = now.AddDays(daysUntilMonday);
            var nextExpiration = new DateTimeOffset(nextMonday.Year, nextMonday.Month, nextMonday.Day, 3, 0, 0, 0, 0,
                nextMonday.Offset);
            var scores = await weeklyTournies.GetEntries(null, context.CancellationToken);
            foreach (var chartGroup in scores.GroupBy(s => s.ChartId))
            {
                var inRangeLeaderboard = ProcessIntoPlaces(chartGroup.Where(c => c.WasWithinRange)).ToArray();
                await weeklyTournies.WriteHistories(
                    inRangeLeaderboard.Select(e => new UserTourneyHistory(e.Item2.UserId, e.Item2.ChartId, now, e.Item1,
                        true,
                        e.Item2.Score, e.Item2.Plate, e.Item2.IsBroken)), context.CancellationToken);
                var existing = inRangeLeaderboard.Select(e => e.Item2.UserId).Distinct().ToHashSet();
                var totalLeaderboard = ProcessIntoPlaces(chartGroup);
                await weeklyTournies.WriteHistories(
                    totalLeaderboard.Where(e => !existing.Contains(e.Item2.UserId)).Select(e =>
                        new UserTourneyHistory(e.Item2.UserId, e.Item2.ChartId, now, e.Item1, false,
                            e.Item2.Score, e.Item2.Plate, e.Item2.IsBroken)), context.CancellationToken);
            }

            await weeklyTournies.ClearTheBoard(context.CancellationToken);

            var alreadyPlayed = (await weeklyTournies.GetAlreadyPlayedCharts(context.CancellationToken)).Distinct()
                .ToHashSet();
            var random = new Random();
            var newCharts = new HashSet<Guid>();
            var chartDict = (await charts.GetCharts(MixEnum.Phoenix, cancellationToken: context.CancellationToken))
                .ToDictionary(c => c.Id);
            var buckets = chartDict.Values
                .Where(c => c.Type == ChartType.CoOp || c.Level >= 10)
                .GroupBy(c => (c.Level, c.Type))
                .ToDictionary(g => g.Key, g => g.Select(c => c.Id).Distinct().ToHashSet());
            //Combine CoOp 4-5 into CoOp 3
            for (var players = 4; players <= 5; players++)
            {
                foreach (var chartId in buckets[(players, ChartType.CoOp)]) buckets[(3, ChartType.CoOp)].Add(chartId);

                buckets.Remove((players, ChartType.CoOp));
            }

            //Move Paradoxx into S25s
            foreach (var chart in buckets[(26, ChartType.Single)]) buckets[(25, ChartType.Single)].Add(chart);

            buckets.Remove((26, ChartType.Single));


            foreach (var bucket in buckets)
            {
                var chartsInRange = bucket.Value.Select(c => chartDict[c]).ToArray();

                if (!chartsInRange.Any()) continue;
                var validCharts = chartsInRange.Where(r => !alreadyPlayed.Contains(r.Id)).ToArray();
                if (validCharts.Any())
                {
                    validCharts = chartsInRange;
                    await weeklyTournies.ClearAlreadyPlayedCharts(chartsInRange.Select(c => c.Id),
                        context.CancellationToken);
                }

                var nextChart = validCharts.OrderBy(r => random.Next(1000)).First();
                newCharts.Add(nextChart.Id);
                await weeklyTournies.RegisterWeeklyChart(new WeeklyTournamentChart(nextChart.Id, nextExpiration),
                    context.CancellationToken);
            }

            await weeklyTournies.WriteAlreadyPlayedCharts(newCharts, context.CancellationToken);
        }

        public static IEnumerable<(int, WeeklyTournamentEntry)> ProcessIntoPlaces(
            IEnumerable<WeeklyTournamentEntry> entries)
        {
            var place = 1;
            foreach (var scoreGroup in entries.GroupBy(e => e.Score).OrderByDescending(g => g.Key))
            {
                foreach (var score in scoreGroup) yield return (place, score);
                place += scoreGroup.Count();
            }
        }

        public async Task Handle(RegisterWeeklyChartScore request, CancellationToken cancellationToken)
        {
            var weeklyCharts = (await weeklyTournies.GetWeeklyCharts(cancellationToken)).Select(c => c.ChartId)
                .Distinct()
                .ToHashSet();
            if (!weeklyCharts.Contains(request.Entry.ChartId)) return;

            var chart = (await charts.GetCharts(MixEnum.Phoenix, chartIds: new[] { request.Entry.ChartId },
                    cancellationToken: cancellationToken))
                .Single();
            var stats = await playerStats.GetStats(request.Entry.UserId, cancellationToken);
            var competitiveLevel = chart.Type == ChartType.Single ? stats.SinglesCompetitiveLevel :
                chart.Type == ChartType.Double ? stats.DoublesCompetitiveLevel : stats.CompetitiveLevel;
            var isInRange =
                chart.Type == ChartType.CoOp || competitiveLevel -2.0<=(double)(int)chart.Level;
            var existingEntries = (await weeklyTournies.GetEntries(request.Entry.ChartId, cancellationToken)).ToArray();
            var existingEntry =
                existingEntries.FirstOrDefault(u =>
                    u.UserId == request.Entry.UserId);
            var existingPlace = ProcessIntoPlaces(existingEntries.Where(e => e.WasWithinRange || !isInRange))
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

                if (!request.Entry.WasWithinRange && isInRange)
                    existingEntry = existingEntry with { WasWithinRange = true };
                existingEntry = existingEntry with { PhotoUrl = request.Entry.PhotoUrl };
                await weeklyTournies.SaveEntry(existingEntry, cancellationToken);
            }
            else
            {
                existingEntry = request.Entry with { WasWithinRange = isInRange };
                await weeklyTournies.SaveEntry(existingEntry, cancellationToken);
            }

            var newPlace = ProcessIntoPlaces(existingEntries.Where(u => u.UserId != request.Entry.UserId)
                .Append(existingEntry)).First(e => e.Item2.UserId == request.Entry.UserId).Item1;
            if (existingPlace == null || existingPlace != newPlace)
            {
                var user = await users.GetUser(request.Entry.UserId, cancellationToken);
                try
                {
                    await bot.SendMessage(
                        $"{user.Name} Progressed to {newPlace} on {chart.Song.Name} #DIFFICULTY|{chart.DifficultyString}# - {existingEntry.Score} #LETTERGRADE|{existingEntry.Score.LetterGrade}|{existingEntry.IsBroken}# #PLATE|{existingEntry.Plate}#",
                        1254418262406725773, cancellationToken);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Couldn't post weekly charts update to discord");
                }
            }
        }
    }
}
