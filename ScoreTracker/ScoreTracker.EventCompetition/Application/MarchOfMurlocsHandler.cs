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

        /// <summary>
        ///     Every board a season carries (D3, D38): both chart types on every mix that has the
        ///     section. Doubles first, the order the season page shows them in.
        /// </summary>
        private static readonly IReadOnlyList<MoMBoardKey> Boards = Enum.GetValues<MixEnum>()
            .Where(mix => mix.HasMarchOfMurlocs())
            .SelectMany(mix => new[] { new MoMBoardKey(mix, ChartType.Double), new MoMBoardKey(mix, ChartType.Single) })
            .ToArray();

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
            {
                await _bus.Publish(new CycleMoMCommand());
                return;
            }

            // D43: the live season is healed on every run — any of its four boards that is
            // missing is seated now, with that mix's snapshot taken today. As stateless as the
            // rest of this consumer: the only question is which boards exist.
            var held = await _mom.GetBoardKeys(current.Id, context.CancellationToken);
            var missing = Boards.Where(board => !held.Contains(board)).ToArray();
            if (missing.Length > 0)
                await _mom.AddBoards(current.Id, await SeedBoards(current, missing, context.CancellationToken),
                    context.CancellationToken);

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

            var boards = await SeedBoards(season, Boards, context.CancellationToken);
            await _mom.CreateSeason(season, boards, context.CancellationToken);
        }

        /// <summary>
        ///     The boards to seat: one per requested (mix, chart type), priced on that mix's
        ///     PUMBILITY+ and carrying that mix's snapshot deltas — the balanced level per chart,
        ///     kept only where it differs from the folder level + 0.5, because the no-row fallback
        ///     is byte-identical to storing that value (§9.3).
        /// </summary>
        private async Task<IReadOnlyList<MoMBoardSeed>> SeedBoards(MoMSeason season,
            IEnumerable<MoMBoardKey> wanted, CancellationToken cancellationToken)
        {
            var boards = new List<MoMBoardSeed>();
            foreach (var ofMix in wanted.GroupBy(board => board.Mix))
            {
                var charts = (await _charts.GetCharts(ofMix.Key)).Where(c => c.Type != ChartType.CoOp).ToArray();
                var scoringLevels = await _scoringLevels.GetScoringLevels(ofMix.Key, cancellationToken);
                foreach (var board in ofMix)
                {
                    var boardId = Guid.NewGuid();
                    var configuration = new TournamentConfiguration(boardId, BoardName(season, board),
                        MoMScoring.ForBoard(board.Mix, board.ChartType), true, true)
                    {
                        AllowRepeats = false,
                        EndDate = season.EndsAt,
                        StartDate = season.StartsAt,
                        MaxTime = MoMScoring.Window
                    };

                    var deltas = new Dictionary<Guid, double>();
                    foreach (var chart in charts.Where(c => c.Type == board.ChartType))
                    {
                        var floor = chart.Level + .5;
                        var balanced = MoMScoring.BalancedLevel((int)chart.Level,
                            scoringLevels.TryGetValue(chart.Id, out var scoringLevel) ? scoringLevel : null);
                        if (Math.Abs(balanced - floor) > 0.0001) deltas[chart.Id] = balanced;
                    }

                    boards.Add(new MoMBoardSeed(boardId, board.Mix, board.ChartType, configuration, deltas));
                }
            }

            return boards;
        }

        /// <summary>Phoenix keeps the name the legacy listing always showed; another mix says which it is.</summary>
        private static string BoardName(MoMSeason season, MoMBoardKey board)
        {
            var name = $"March of Murlocs {season.Name} - {board.ChartType}s";
            return board.Mix == MixEnum.Phoenix ? name : $"{name} ({board.Mix.GetName()})";
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
