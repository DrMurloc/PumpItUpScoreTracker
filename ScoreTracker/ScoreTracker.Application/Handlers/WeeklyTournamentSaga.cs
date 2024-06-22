using MassTransit;
using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class WeeklyTournamentSaga
        (IChartRepository charts, IWeeklyTournamentRepository weeklyTournies, IPlayerStatsRepository playerStats) :
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
            for (var level = 1; level <= 28; level++)
                foreach (var chartType in new[] { ChartType.Single, ChartType.Double, ChartType.CoOp })
                {
                    var chartsInRange = (await charts.GetCharts(MixEnum.Phoenix, level, chartType,
                        cancellationToken: context.CancellationToken)).ToArray();
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
                chart.Type == ChartType.CoOp || Math.Abs(competitiveLevel - (int)chart.Level) <= 1.0;

            var existingEntry =
                (await weeklyTournies.GetEntries(request.Entry.ChartId, cancellationToken)).FirstOrDefault(u =>
                    u.UserId == request.Entry.UserId);
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

                await weeklyTournies.SaveEntry(existingEntry, cancellationToken);
            }
            else
            {
                await weeklyTournies.SaveEntry(request.Entry with { WasWithinRange = isInRange }, cancellationToken);
            }
        }
    }
}
