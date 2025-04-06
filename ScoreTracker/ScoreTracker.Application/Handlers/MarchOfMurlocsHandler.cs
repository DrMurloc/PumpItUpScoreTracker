using MassTransit;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class MarchOfMurlocsHandler : IConsumer<MarchOfMurlocsHandler.TryScheduleMoM>,
        IConsumer<MarchOfMurlocsHandler.CycleMoM>
    {
        private ITournamentRepository _tournaments;
        private IChartRepository _charts;
        private IBus _bus;
        private readonly IMessageScheduler _scheduler;

        public MarchOfMurlocsHandler(ITournamentRepository tournaments,
            IChartRepository charts,
            IBus bus,
            IMessageScheduler scheduler)
        {
            _tournaments = tournaments;
            _charts = charts;
            _bus = bus;
            _scheduler = scheduler;
        }

        public sealed record TryScheduleMoM
        {
        }

        public sealed record CycleMoM
        {
        }

        public async Task Consume(ConsumeContext<TryScheduleMoM> context)
        {
            var mom = (await _tournaments.GetAllTournaments(context.CancellationToken)).FirstOrDefault(e => e.IsMoM);
            if (mom?.EndDate == null || mom.EndDate < DateTimeOffset.Now)
                await _bus.Publish(new CycleMoM());
            else
                await _scheduler.SchedulePublish((mom.EndDate.Value + TimeSpan.FromMinutes(1)).DateTime, new CycleMoM(),
                    context.CancellationToken);
        }

        public async Task Consume(ConsumeContext<CycleMoM> context)
        {
            var moms = (await _tournaments.GetAllTournaments(context.CancellationToken)).Where(e => e.IsMoM).ToArray();
            var oldEnd = moms.FirstOrDefault()?.EndDate ?? DateTimeOffset.Now - TimeSpan.FromMinutes(1);
            var year = DateTimeOffset.Now.Year;

            var newMonth = oldEnd.Month switch

            {
                12 => 3,
                1 => 3,
                2 => 3,
                3 => 6,
                4 => 6,
                5 => 6,
                7 => 9,
                8 => 9,
                9 => 12,
                10 => 12,
                11 => 12,
                _ => throw new ArgumentOutOfRangeException("Date was invalid somehow?")
            };
            var season = newMonth switch
            {
                3 => "Winter",
                6 => "Spring",
                9 => "Summer",
                12 => "Fall",
                _ => throw new ArgumentOutOfRangeException("Date was invalid somehow 2?")
            };
            var newEndDate = new DateTimeOffset(new DateTime(DateTimeOffset.Now.Year, newMonth,
                DateTime.DaysInMonth(DateTimeOffset.Now.Year, newMonth),
                23, 59, 59), TimeSpan.FromHours(-5));

            var charts = (await _charts.GetCharts(MixEnum.Phoenix)).Where(c => c.Type != ChartType.CoOp).ToArray();

            foreach (var chartType in new[] { ChartType.Double, ChartType.Single })
            {
                var scoring = ScoringConfiguration.PumbilityPlus;
                scoring.AdjustToTime = true;
                scoring.LevelRatings[22] += 50;
                scoring.LevelRatings[23] += 150;
                scoring.LevelRatings[24] += 300;
                scoring.LevelRatings[25] += 500;
                scoring.LevelRatings[26] += 750;
                scoring.LevelRatings[27] += 1050;
                scoring.LevelRatings[28] += 1400;
                scoring.LevelRatings[29] += 1800;
                foreach (var key in scoring.ChartTypeModifiers.Keys)
                {
                    if (key == chartType) continue;

                    scoring.ChartTypeModifiers[key] = 0;
                }

                var tournament = new TournamentConfiguration(Guid.NewGuid(),
                    $"March of Murlocs {season} {year} - {chartType}s",
                    scoring, false, true)
                {
                    AllowRepeats = false,
                    EndDate = newEndDate,
                    StartDate = DateTimeOffset.Now,
                    MaxTime = TimeSpan.FromHours(1) + TimeSpan.FromMinutes(45)
                };

                var curCharts = charts.Where(c => c.Type == chartType).ToArray();
                var levels = curCharts.Select(c => (c.Id,
                        c.ScoringLevel == null ? c.Level + .5 :
                        c.Level + 1.5 < c.ScoringLevel ? c.Level + 1.5 :
                        c.Level + .5 < c.ScoringLevel ? c.ScoringLevel.Value :
                        c.Level + .5
                    )).ToArray();
                await _tournaments.CreateOrSaveTournament(tournament, context.CancellationToken);

                await _tournaments.CreateScoringLevelSnapshots(tournament.Id, levels, context.CancellationToken);
            }


            foreach (var mom in moms)
            {
                var updated = mom with { IsHighlighted = false };
                await _tournaments.CreateOrSaveTournament(updated, context.CancellationToken);
            }
        }
    }
}
