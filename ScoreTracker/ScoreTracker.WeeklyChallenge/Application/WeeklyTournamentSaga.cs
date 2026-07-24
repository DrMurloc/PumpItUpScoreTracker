using ScoreTracker.Domain.Services;
using ScoreTracker.WeeklyChallenge.Contracts;
using ScoreTracker.WeeklyChallenge.Contracts.Events;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;
using ScoreTracker.WeeklyChallenge.Contracts.Messages;
using ScoreTracker.WeeklyChallenge.Contracts.Commands;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
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
        ILogger<WeeklyTournamentSaga> logger, IUserReader users, IBus bus,
        IDateTimeOffsetAccessor dateTime, IRandomNumberGenerator random, IMemoryCache cache) :
        IConsumer<RotateWeeklyChartsCommand>,
        IConsumer<ScoreImportCompletedEvent>,
        IRequestHandler<RegisterWeeklyChartScoreCommand>,
        IRequestHandler<GetWeeklyChartsQuery, IEnumerable<WeeklyTournamentChart>>,
        IRequestHandler<GetWeeklyChartEntriesQuery, IEnumerable<WeeklyTournamentEntry>>,
        IRequestHandler<GetPastWeeklyEntriesQuery, IEnumerable<WeeklyTournamentEntry>>,
        IRequestHandler<GetPastWeeklyDatesQuery, IEnumerable<DateTimeOffset>>,
        IRequestHandler<GetAlreadyPlayedWeeklyChartsQuery, IEnumerable<Guid>>,
        IRequestHandler<GetUserWeeklyPlacementsQuery, IEnumerable<WeeklyPlacementRecord>>,
        IRequestHandler<GetWeeklyBoardQuery, WeeklyBoardView>,
        IRequestHandler<GetMonthlyLeaderboardQuery, MonthlyLeaderboardView>,
        IRequestHandler<GetWeeklyChartBoardQuery, IReadOnlyList<WeeklyBoardRow>>
    {
        // One chart's full ranked board with trust sources — the challenges page's leaderboard
        // dialog read. The shared LeaderboardDialog re-ranks and looks players up itself, so this
        // exists for the source per row (the ✔/📷 ladder); the plain entries query can't carry it.
        public async Task<IReadOnlyList<WeeklyBoardRow>> Handle(GetWeeklyChartBoardQuery request,
            CancellationToken cancellationToken)
        {
            var withSources = (await weeklyTournies.GetEntriesWithSources(request.Mix, request.ChartId,
                cancellationToken)).ToArray();
            var entries = withSources.Select(e => e.Entry).ToArray();
            var sources = withSources.ToDictionary(e => e.Entry.UserId, e => e.Source);
            var ranked = WeeklyChartSuggestionPolicy.ProcessIntoPlaces(entries).ToArray();
            var chart = (await charts.GetCharts(request.Mix, chartIds: new[] { request.ChartId },
                cancellationToken: cancellationToken)).FirstOrDefault();
            var inRangePlaces = WeeklyChartSuggestionPolicy.ProcessIntoPlaces(chart == null
                    ? entries
                    : entries.Where(e => WeeklyChartSuggestionPolicy.IsWithinRange(chart, e.CompetitiveLevel)))
                .ToDictionary(r => r.Item2.UserId, r => r.Item1);
            var userDict = (await users.GetUsers(ranked.Select(r => r.Item2.UserId).Distinct().ToArray(),
                cancellationToken)).ToDictionary(u => u.Id);
            return ranked.Select(r => new WeeklyBoardRow(r.Item1, userDict.GetValueOrDefault(r.Item2.UserId),
                r.Item2, sources.TryGetValue(r.Item2.UserId, out var s) ? s : null,
                chart == null || WeeklyChartSuggestionPolicy.IsWithinRange(chart, r.Item2.CompetitiveLevel),
                inRangePlaces.TryGetValue(r.Item2.UserId, out var inRangePlace) ? inRangePlace : null)).ToList();
        }

        // The challenges page's board read: ranked heads + the caller's standing per chart,
        // display-enriched through IUserReader so the page dispatches one query instead of a
        // charts + entries + identity cascade.
        public async Task<WeeklyBoardView> Handle(GetWeeklyBoardQuery request, CancellationToken cancellationToken)
        {
            var isLive = request.WeekStart == null;
            IReadOnlyList<WeeklyTournamentChart> boardCharts;
            WeeklyTournamentEntry[] entries;
            // Trust sources exist on the live table only — finished weeks read from the
            // histories, which don't carry them, so past rows render tagless.
            var sources = new Dictionary<(Guid UserId, Guid ChartId), ChallengeEntrySource>();
            if (isLive)
            {
                boardCharts = (await weeklyTournies.GetWeeklyCharts(request.Mix, cancellationToken)).ToArray();
                var withSources =
                    (await weeklyTournies.GetEntriesWithSources(request.Mix, null, cancellationToken)).ToArray();
                entries = withSources.Select(e => e.Entry).ToArray();
                foreach (var (entry, source) in withSources) sources[(entry.UserId, entry.ChartId)] = source;
            }
            else
            {
                entries = (await weeklyTournies.GetPastEntries(request.Mix, request.WeekStart!.Value,
                    cancellationToken)).ToArray();
                // A finished week has no chart table of its own — its charts are whatever got played.
                boardCharts = entries.Select(e => e.ChartId).Distinct()
                    .Select(id => new WeeklyTournamentChart(id, request.WeekStart!.Value)).ToArray();
            }

            if (request.OnlyUserIds is { Count: > 0 })
            {
                var allowed = request.OnlyUserIds.ToHashSet();
                entries = entries.Where(e => allowed.Contains(e.UserId)).ToArray();
            }

            var byChart = entries.GroupBy(e => e.ChartId).ToDictionary(g => g.Key, g => g.ToArray());
            var ranked = boardCharts.ToDictionary(c => c.ChartId, c =>
                WeeklyChartSuggestionPolicy.ProcessIntoPlaces(
                    byChart.TryGetValue(c.ChartId, out var chartEntries)
                        ? chartEntries
                        : Array.Empty<WeeklyTournamentEntry>()).ToArray());

            // The relevant-players ladder (M20): the same boards with out-of-band entries
            // removed and places renumbered. A chart the catalog can't resolve filters nothing.
            var chartDict = (await charts.GetCharts(request.Mix,
                    chartIds: boardCharts.Select(c => c.ChartId).ToArray(),
                    cancellationToken: cancellationToken))
                .ToDictionary(c => c.Id);
            var inRangeRanked = boardCharts.ToDictionary(c => c.ChartId, c =>
            {
                var chart = chartDict.GetValueOrDefault(c.ChartId);
                var chartEntries = byChart.TryGetValue(c.ChartId, out var es)
                    ? es
                    : Array.Empty<WeeklyTournamentEntry>();
                return WeeklyChartSuggestionPolicy.ProcessIntoPlaces(chart == null
                    ? chartEntries
                    : chartEntries.Where(e =>
                        WeeklyChartSuggestionPolicy.IsWithinRange(chart, e.CompetitiveLevel))).ToArray();
            });

            // Suggestion flags only mean something on the live board, and only for a caller with
            // calibrated competitive levels.
            var suggested = new HashSet<Guid>();
            var suggestionsAvailable = false;
            if (isLive && request.UserId != null && boardCharts.Any())
            {
                var stats = await playerStats.GetStats(request.Mix, request.UserId.Value, cancellationToken);
                suggestionsAvailable = stats is { DoublesCompetitiveLevel: >= 10, SinglesCompetitiveLevel: >= 10 };
                if (suggestionsAvailable)
                    suggested = WeeklyChartSuggestionPolicy.GetSuggestedCharts(
                            boardCharts.Where(c => chartDict.ContainsKey(c.ChartId))
                                .Select(c => chartDict[c.ChartId]),
                            stats.DoublesCompetitiveLevel, stats.SinglesCompetitiveLevel)
                        .Select(c => c.Id).ToHashSet();
            }

            var visibleUserIds = ranked.Values.Concat(inRangeRanked.Values)
                .SelectMany(r => r.Take(3).Select(row => row.Item2.UserId))
                .Concat(request.UserId is { } caller ? new[] { caller } : Array.Empty<Guid>())
                .Distinct().ToArray();
            var userDict = (await users.GetUsers(visibleUserIds, cancellationToken)).ToDictionary(u => u.Id);

            var summaries = boardCharts.Select(chart =>
            {
                var chartRanked = ranked[chart.ChartId];
                var chartInRange = inRangeRanked[chart.ChartId];
                // Place is the OVERALL place for every row, including the in-range-top rows. The grid
                // merges the two heads and orders by Place (WeeklyBoardGrid.MergedTop); an in-range
                // row carrying its own renumbered place would sort into the overall ladder by the
                // wrong number — a 947k in-range #2 landing above an 989k overall #3.
                var overallPlaces = chartRanked.ToDictionary(r => r.Item2.UserId, r => r.Item1);
                var inRangePlaces = chartInRange.ToDictionary(r => r.Item2.UserId, r => r.Item1);
                var catalogChart = chartDict.GetValueOrDefault(chart.ChartId);
                WeeklyBoardRow ToRow((int, WeeklyTournamentEntry) r) => new(
                    overallPlaces.TryGetValue(r.Item2.UserId, out var op) ? op : r.Item1,
                    userDict.GetValueOrDefault(r.Item2.UserId), r.Item2,
                    sources.TryGetValue((r.Item2.UserId, r.Item2.ChartId), out var source)
                        ? source
                        : null,
                    catalogChart == null || WeeklyChartSuggestionPolicy.IsWithinRange(catalogChart,
                        r.Item2.CompetitiveLevel),
                    inRangePlaces.TryGetValue(r.Item2.UserId, out var inRangePlace) ? inRangePlace : null);
                var top = chartRanked.Take(3).Select(ToRow).ToArray();
                var inRangeTop = chartInRange.Take(3).Select(ToRow).ToArray();
                var mine = request.UserId == null
                    ? null
                    : chartRanked.Where(r => r.Item2.UserId == request.UserId)
                        .Select(ToRow)
                        .FirstOrDefault();
                return new WeeklyBoardChartSummary(chart.ChartId, chart.ExpirationDate, chartRanked.Length, top,
                    mine, suggested.Contains(chart.ChartId), inRangeTop, chartInRange.Length);
            }).ToArray();

            return new WeeklyBoardView(InBoardOrder(summaries, chartDict), isLive, suggestionsAvailable);
        }

        // The board arrives in whatever order the week's rows were written — a chart list nobody
        // can scan. WeeklyBoardOrder is the canonical Phoenix 1 order (shared with the homepage
        // widget): level descending, singles before doubles within a level, co-ops last with the
        // 2-player duet last of all.
        private static IReadOnlyList<WeeklyBoardChartSummary> InBoardOrder(
            IEnumerable<WeeklyBoardChartSummary> summaries, IReadOnlyDictionary<Guid, Chart> charts)
        {
            return summaries
                .OrderBy(s => WeeklyBoardOrder.SortKey(charts.GetValueOrDefault(s.ChartId)))
                .ToArray();
        }

        // The monthly board, aggregated and priced here instead of week-by-week in the page.
        // Pricing is the mix's own PUMBILITY (O4, weekly-charts-overhaul.md §6): brokens price
        // at zero per the game; Combined excludes co-op (Phoenix 2's own rule); the Co-Op view
        // ranks raw score — the only currency co-op charts share across a month.
        public async Task<MonthlyLeaderboardView> Handle(GetMonthlyLeaderboardQuery request,
            CancellationToken cancellationToken)
        {
            // The page ships all four type boards statically (§12.5), so every anonymous view
            // costs four dispatches — the unscoped live-window shape caches briefly to keep
            // that flat. Community-scoped and past-window reads stay uncached.
            if (request.OnlyUserIds == null && request.AnchorWeek == null)
                return (await cache.GetOrCreateAsync(
                    $"{nameof(WeeklyTournamentSaga)}:monthly:{request.Mix}:{request.Type}",
                    async e =>
                    {
                        e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
                        return await ComputeMonthlyLeaderboard(request, cancellationToken);
                    }))!;
            return await ComputeMonthlyLeaderboard(request, cancellationToken);
        }

        private async Task<MonthlyLeaderboardView> ComputeMonthlyLeaderboard(GetMonthlyLeaderboardQuery request,
            CancellationToken cancellationToken)
        {
            var now = dateTime.Now;
            // Weeks belong to the month their board started in (rotation date − 7 days).
            var anchorStart = request.AnchorWeek == null ? now : request.AnchorWeek.Value - TimeSpan.FromDays(7);
            var monthDates = (await weeklyTournies.GetPastDates(request.Mix, cancellationToken))
                .Where(d => (d - TimeSpan.FromDays(7)).Year == anchorStart.Year &&
                            (d - TimeSpan.FromDays(7)).Month == anchorStart.Month)
                .ToArray();
            var isCurrentMonth = anchorStart.Year == now.Year && anchorStart.Month == now.Month;

            var entries = new List<WeeklyTournamentEntry>();
            entries.AddRange(await weeklyTournies.GetPastEntries(request.Mix, monthDates, cancellationToken));
            DateTimeOffset? liveWeekStart = null;
            if (isCurrentMonth)
            {
                var liveCharts = (await weeklyTournies.GetWeeklyCharts(request.Mix, cancellationToken)).ToArray();
                if (liveCharts.Any()) liveWeekStart = liveCharts.Max(c => c.ExpirationDate) - TimeSpan.FromDays(7);
                entries.AddRange(await weeklyTournies.GetEntries(request.Mix, null, cancellationToken));
            }

            var weekInMonth = monthDates.Length + (isCurrentMonth ? 1 : 0);
            var countedPerPlayer = 4 * weekInMonth;
            DateTimeOffset? windowStart = monthDates.Any() ? monthDates.Min() - TimeSpan.FromDays(7) : liveWeekStart;
            DateTimeOffset? windowEnd = isCurrentMonth ? null :
                monthDates.Any() ? monthDates.Max() - TimeSpan.FromDays(1) : null;

            if (request.OnlyUserIds is { Count: > 0 })
            {
                var allowed = request.OnlyUserIds.ToHashSet();
                entries = entries.Where(e => allowed.Contains(e.UserId)).ToList();
            }

            if (!entries.Any())
                return new MonthlyLeaderboardView(Array.Empty<MonthlyLeaderboardRow>(), weekInMonth,
                    countedPerPlayer, windowStart, windowEnd);

            var chartDict = (await charts.GetCharts(request.Mix,
                    chartIds: entries.Select(e => e.ChartId).Distinct().ToArray(),
                    cancellationToken: cancellationToken))
                .ToDictionary(c => c.Id);
            entries = entries.Where(e => chartDict.TryGetValue(e.ChartId, out var chart) &&
                                         (request.Type == null
                                             ? chart.Type != ChartType.CoOp
                                             : chart.Type == request.Type)).ToList();

            var scoring = ScoringConfiguration.PumbilityScoring(request.Mix, false);
            double Price(WeeklyTournamentEntry entry)
            {
                var chart = chartDict[entry.ChartId];
                return request.Type == ChartType.CoOp
                    ? (int)entry.Score
                    : scoring.GetScore(chart.Type, chart.Level, entry.Score, entry.Plate, entry.IsBroken);
            }

            var totals = entries.GroupBy(e => e.UserId).Select(g =>
                {
                    var counted = g
                        .Select(e => new MonthlyEntry(e.ChartId, e.Score, e.Plate, e.IsBroken, Price(e)))
                        .OrderByDescending(m => m.Points).ThenByDescending(m => (int)m.Score)
                        .Take(countedPerPlayer).ToArray();
                    return (UserId: g.Key, Counted: counted,
                        Total: counted.Sum(m => m.Points),
                        RawSum: counted.Sum(m => (int)m.Score),
                        CompetitiveLevel: g.Max(e => e.CompetitiveLevel));
                })
                // Stepped grade multipliers tie more often than the old continuous scale;
                // raw-score sum breaks them (§6).
                .OrderByDescending(r => r.Total).ThenByDescending(r => r.RawSum)
                .ToArray();

            var userDict = (await users.GetUsers(totals.Select(t => t.UserId).ToArray(), cancellationToken))
                .ToDictionary(u => u.Id);
            var rows = totals.Select((r, i) => new MonthlyLeaderboardRow(i + 1,
                    userDict.GetValueOrDefault(r.UserId), r.Total,
                    r.Counted.Take(4).ToArray(), r.Counted, r.CompetitiveLevel))
                .ToArray();

            return new MonthlyLeaderboardView(rows, weekInMonth, countedPerPlayer, windowStart, windowEnd);
        }

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
                // The snapshot stamps each row's band verdict from the entry's stored
                // competitive level — the same value SaveEntry judged, so the two never drift.
                var chart = chartDict.GetValueOrDefault(chartGroup.Key);
                var leaderboard = WeeklyChartSuggestionPolicy.ProcessIntoPlaces(chartGroup).ToArray();
                await weeklyTournies.WriteHistories(mix,
                    leaderboard.Select(e => new UserTourneyHistory(e.Item2.UserId, e.Item2.ChartId, now, e.Item1,
                        e.Item2.CompetitiveLevel,
                        e.Item2.Score, e.Item2.Plate, e.Item2.IsBroken,
                        chart != null && WeeklyChartSuggestionPolicy.IsWithinRange(chart,
                            e.Item2.CompetitiveLevel))), context.CancellationToken);
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
                            Enum.Parse<PhoenixPlate>(score.Plate), score.IsBroken, null, 10.0), mix,
                        ChallengeEntrySource.Official),
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
            var wasWithinRange = WeeklyChartSuggestionPolicy.IsWithinRange(chart, competitiveLevel);

            var existingWithSources =
                (await weeklyTournies.GetEntriesWithSources(mix, request.Entry.ChartId, cancellationToken))
                .ToArray();
            var existingEntries = existingWithSources.Select(e => e.Entry).ToArray();
            var existingPair = existingWithSources.FirstOrDefault(u => u.Entry.UserId == request.Entry.UserId);
            var existingEntry = existingPair.Entry;
            var existingPlace = WeeklyChartSuggestionPolicy.ProcessIntoPlaces(existingEntries)
                .Where(u => u.Item2.UserId == request.Entry.UserId)
                .Select(u => (int?)u.Item1).FirstOrDefault();

            if (existingEntry != null)
            {
                // The source tag describes the ranked score's provenance — it only moves when
                // the score does (a weaker manual submit never demotes a verified score).
                var entrySource = existingPair.Source;
                if (request.Entry.Score > existingEntry.Score)
                {
                    existingEntry = existingEntry with { Score = request.Entry.Score };
                    entrySource = request.Source;
                }

                if (request.Entry.Plate > existingEntry.Plate)
                    existingEntry = existingEntry with { Plate = request.Entry.Plate };

                if (!request.Entry.IsBroken && existingEntry.IsBroken)
                    existingEntry = existingEntry with { IsBroken = false };

                existingEntry = existingEntry with { CompetitiveLevel = competitiveLevel };
                // Photos are optional proof (M3): a photo-less submit must not wipe one already attached.
                existingEntry = existingEntry with { PhotoUrl = request.Entry.PhotoUrl ?? existingEntry.PhotoUrl };
                await weeklyTournies.SaveEntry(mix, existingEntry, entrySource, wasWithinRange, cancellationToken);
            }
            else
            {
                existingEntry = request.Entry with { CompetitiveLevel = competitiveLevel };
                await weeklyTournies.SaveEntry(mix, existingEntry, request.Source, wasWithinRange, cancellationToken);
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
