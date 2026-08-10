using MassTransit;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts.Messages;

namespace ScoreTracker.EventCompetition.Application
{
    internal sealed class MarchOfMurlocsHandler : IConsumer<TryScheduleMoMCommand>,
        IConsumer<CycleMoMCommand>
    {
        private ITournamentRepository _tournaments;
        private IChartRepository _charts;
        private IBus _bus;
        private readonly IMessageScheduler _scheduler;
        private readonly IDateTimeOffsetAccessor _dateTime;
        private readonly IChartScoringLevelRepository _scoringLevels;

        public MarchOfMurlocsHandler(ITournamentRepository tournaments,
            IChartRepository charts,
            IBus bus,
            IMessageScheduler scheduler,
            IDateTimeOffsetAccessor dateTime,
            IChartScoringLevelRepository scoringLevels)
        {
            _scoringLevels = scoringLevels;
            _tournaments = tournaments;
            _charts = charts;
            _bus = bus;
            _scheduler = scheduler;
            _dateTime = dateTime;
        }

        public async Task Consume(ConsumeContext<TryScheduleMoMCommand> context)
        {
            // Pick the most recent MoM, not any-old-MoM. The previous FirstOrDefault was the
            // root cause of the runaway: once any expired MoM existed, every TryScheduleMoMCommand tick
            // saw it and immediately fired CycleMoMCommand, regardless of whether a current MoM was active.
            var mom = (await _tournaments.GetAllTournaments(context.CancellationToken))
                .Where(e => e.IsMoM)
                .OrderByDescending(e => e.EndDate)
                .FirstOrDefault();
            if (mom?.EndDate == null || mom.EndDate < _dateTime.Now)
                await _bus.Publish(new CycleMoMCommand());
            else
                await _scheduler.SchedulePublish((mom.EndDate.Value + TimeSpan.FromMinutes(1)).DateTime, new CycleMoMCommand(),
                    context.CancellationToken);
        }

        /// <summary>
        ///     The month a season ends in, given the month the previous season ended in. Seasons
        ///     are quarters ending in March, June, September and December, so the answer is always
        ///     three months on, wrapping December back to March.
        ///     <para>
        ///         Arithmetic rather than a month-to-month table on purpose: the table this
        ///         replaces listed eleven of the twelve months and fell through to March for the
        ///         twelfth. A season ending in June therefore came back as one ending in March —
        ///         already past — and a past-dated season re-triggers this consumer on every tick.
        ///     </para>
        /// </summary>
        private static int NextQuarterEndMonth(int month)
        {
            return month / 3 % 4 * 3 + 3;
        }

        /// <summary>
        ///     The instant a season closes: the last moment of the last day of its final month,
        ///     in UTC-5.
        /// </summary>
        private static DateTimeOffset EndOfSeason(int year, int month)
        {
            return new DateTimeOffset(
                new DateTime(year, month, DateTime.DaysInMonth(year, month), 23, 59, 59),
                TimeSpan.FromHours(-5));
        }

        public async Task Consume(ConsumeContext<CycleMoMCommand> context)
        {
            var moms = (await _tournaments.GetAllTournaments(context.CancellationToken))
                .Where(e => e.IsMoM)
                .OrderByDescending(e => e.EndDate)
                .ToArray();
            // Idempotency: if a future-dated MoM already exists, this cycle has nothing to do.
            // Protects against duplicate CycleMoMCommand messages (in-memory transport replay, double-publish, etc.)
            // landing back-to-back and creating extra tournaments.
            if (moms.Any(m => m.EndDate != null && m.EndDate > _dateTime.Now))
                return;
            var oldEnd = moms.FirstOrDefault()?.EndDate ?? _dateTime.Now - TimeSpan.FromMinutes(1);

            // The season that follows the last one, advanced until it actually lies ahead of us.
            // Both halves matter. The year comes from the previous season rather than from today,
            // because a December season is followed by a March one in the NEXT year. The loop
            // then covers a cycle that runs late — a missed quarter, downtime, a manual trigger
            // while behind — where the quarter after the last season has itself already ended.
            // Creating that season anyway is what made the runaway self-sustaining: a season
            // ending in the past leaves TryScheduleMoM with no future MoM to wait for, so it
            // publishes another CycleMoMCommand, forever. Catching up lands on the current
            // quarter and creates one season, not a backlog.
            var newYear = oldEnd.Year;
            var newMonth = NextQuarterEndMonth(oldEnd.Month);
            if (newMonth <= oldEnd.Month) newYear++;
            var newEndDate = EndOfSeason(newYear, newMonth);
            while (newEndDate <= _dateTime.Now)
            {
                var previousMonth = newMonth;
                newMonth = NextQuarterEndMonth(previousMonth);
                if (newMonth <= previousMonth) newYear++;
                newEndDate = EndOfSeason(newYear, newMonth);
            }

            var season = newMonth switch
            {
                3 => "Winter",
                6 => "Spring",
                9 => "Summer",
                12 => "Fall",
                _ => throw new ArgumentOutOfRangeException("Date was invalid somehow 2?")
            };

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
                    $"March of Murlocs {season} {newYear} - {chartType}s",
                    // Highlighted: the shell's Compete menu lists HighlightedEvents, so this is
                    // what puts a running season in the nav. The loop at the end of this method
                    // unhighlights the seasons it replaces, which only means anything if the new
                    // ones arrive highlighted.
                    scoring, true, true)
                {
                    AllowRepeats = false,
                    EndDate = newEndDate,
                    StartDate = _dateTime.Now,
                    MaxTime = TimeSpan.FromHours(1) + TimeSpan.FromMinutes(45)
                };

                var curCharts = charts.Where(c => c.Type == chartType).ToArray();
                var scoringLevels =
                    await _scoringLevels.GetScoringLevels(MixEnum.Phoenix, context.CancellationToken);
                var levels = curCharts.Select(c =>
                {
                    double? scoringLevel = scoringLevels.TryGetValue(c.Id, out var sl) ? sl : null;
                    return (c.Id,
                        scoringLevel == null ? c.Level + .5 :
                        c.Level + 1.5 < scoringLevel ? c.Level + 1.5 :
                        c.Level + .5 < scoringLevel ? scoringLevel.Value :
                        c.Level + .5);
                }).ToArray();
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
