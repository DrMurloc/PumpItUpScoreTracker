using MassTransit;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts.Messages;
using ScoreTracker.EventCompetition.Domain;

namespace ScoreTracker.EventCompetition.Application
{
    internal sealed class MarchOfMurlocsHandler : IConsumer<TryScheduleMoMCommand>,
        IConsumer<CycleMoMCommand>
    {
        // Season boundaries are defined in UTC-5 (§1): a quarter ends at 23:59:59 on the last
        // day of March, June, September or December.
        private static readonly TimeSpan SeasonOffset = TimeSpan.FromHours(-5);

        private readonly IMoMRepository _mom;
        private readonly IChartRepository _charts;
        private readonly IBus _bus;
        private readonly IMessageScheduler _scheduler;
        private readonly IDateTimeOffsetAccessor _dateTime;
        private readonly IChartScoringLevelRepository _scoringLevels;

        public MarchOfMurlocsHandler(IMoMRepository mom,
            IChartRepository charts,
            IBus bus,
            IMessageScheduler scheduler,
            IDateTimeOffsetAccessor dateTime,
            IChartScoringLevelRepository scoringLevels)
        {
            _scoringLevels = scoringLevels;
            _mom = mom;
            _charts = charts;
            _bus = bus;
            _scheduler = scheduler;
            _dateTime = dateTime;
        }

        public async Task Consume(ConsumeContext<TryScheduleMoMCommand> context)
        {
            // Stateless on purpose (D2): the only question is whether the quarter we are
            // standing in has its season — history never enters into it, so no backlog of
            // past seasons can re-trigger anything. A season found here always ends in the
            // future, because it ends when its own quarter does.
            var (year, quarter) = CurrentQuarter(_dateTime.Now);
            var current = await _mom.GetSeason(year, quarter, context.CancellationToken);
            if (current == null)
                await _bus.Publish(new CycleMoMCommand());
            else
                // UtcDateTime, not DateTime: the season clock runs UTC-5, and the scheduler
                // compares against UTC — the bare wall-clock time would fire five hours early
                // (harmlessly, but the real rollover then waits for the next daily tick).
                await _scheduler.SchedulePublish((current.EndsAt + TimeSpan.FromMinutes(1)).UtcDateTime,
                    new CycleMoMCommand(), context.CancellationToken);
        }

        public async Task Consume(ConsumeContext<CycleMoMCommand> context)
        {
            var now = _dateTime.Now;
            var (year, quarter) = CurrentQuarter(now);
            // Idempotency for a duplicated CycleMoMCommand; the filtered unique (Year, Quarter)
            // index is the hard guarantee behind it — a twin season is impossible, not merely
            // avoided.
            if (await _mom.GetSeason(year, quarter, context.CancellationToken) != null) return;

            // D13: an ended season that never received a session leaves when its successor
            // arrives. Boards and snapshot rows cascade; sessions block the prune by predicate.
            await _mom.PruneEndedEmptySeasons(now, context.CancellationToken);

            var seasonId = Guid.NewGuid();
            var endsAt = EndOfSeason(year, quarter * 3);
            var season = new MoMSeason(seasonId, year, (byte)quarter,
                $"{SeasonName(quarter)} {year}", now, endsAt, now);

            var charts = (await _charts.GetCharts(MixEnum.Phoenix)).Where(c => c.Type != ChartType.CoOp)
                .ToArray();
            var scoringLevels =
                await _scoringLevels.GetScoringLevels(MixEnum.Phoenix, context.CancellationToken);

            // Phoenix boards only until the scoring session ungates Phoenix 2 (D12, Slice 5).
            var boards = new List<MoMBoardSeed>();
            foreach (var chartType in new[] { ChartType.Double, ChartType.Single })
            {
                // PumbilityPlus returns a fresh instance, so the MoM-only overrides below stay
                // out of PlayerRatingSaga's stored stat and the public v1 API (§9.5).
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

                var boardId = Guid.NewGuid();
                var configuration = new TournamentConfiguration(boardId,
                    $"March of Murlocs {season.Name} - {chartType}s",
                    scoring, true, true)
                {
                    AllowRepeats = false,
                    EndDate = endsAt,
                    StartDate = now,
                    MaxTime = TimeSpan.FromHours(1) + TimeSpan.FromMinutes(45)
                };

                // The balanced level per chart, kept only where it differs from the folder
                // level + 0.5 — the no-row fallback is byte-identical to storing that value
                // (§9.3), so those rows never exist.
                var deltas = new Dictionary<Guid, double>();
                foreach (var chart in charts.Where(c => c.Type == chartType))
                {
                    // The community scoring level, clamped to at most one level above the
                    // folder and never below the folder's own + 0.5; a chart with no scoring
                    // level sits at the floor.
                    var floor = chart.Level + .5;
                    var balanced = scoringLevels.TryGetValue(chart.Id, out var scoringLevel)
                        ? Math.Clamp(scoringLevel, floor, chart.Level + 1.5)
                        : floor;
                    if (Math.Abs(balanced - floor) > 0.0001) deltas[chart.Id] = balanced;
                }

                boards.Add(new MoMBoardSeed(boardId, MixEnum.Phoenix, chartType, configuration, deltas));
            }

            await _mom.CreateSeason(season, boards, context.CancellationToken);
        }

        /// <summary>The quarter the given instant falls in, on the season clock (UTC-5).</summary>
        private static (int Year, int Quarter) CurrentQuarter(DateTimeOffset now)
        {
            var local = now.ToOffset(SeasonOffset);
            return (local.Year, (local.Month - 1) / 3 + 1);
        }

        private static string SeasonName(int quarter)
        {
            return quarter switch
            {
                1 => "Winter",
                2 => "Spring",
                3 => "Summer",
                4 => "Fall",
                _ => throw new ArgumentOutOfRangeException(nameof(quarter), quarter, "Quarters run 1..4")
            };
        }

        /// <summary>
        ///     The instant a season closes: the last moment of the last day of its final month,
        ///     in UTC-5.
        /// </summary>
        private static DateTimeOffset EndOfSeason(int year, int month)
        {
            return new DateTimeOffset(
                new DateTime(year, month, DateTime.DaysInMonth(year, month), 23, 59, 59),
                SeasonOffset);
        }
    }
}
