using ScoreTracker.Domain.Services;
using MediatR;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.PlayerProgress.Contracts.Queries;

namespace ScoreTracker.PlayerProgress.Application
{
    internal sealed class
        RecommendedChartsSaga : IRequestHandler<GetRecommendedChartsQuery, IEnumerable<ChartRecommendation>>,
            IRequestHandler<SubmitFeedbackCommand>
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IFeedbackRepository _feedback;
        private readonly IPlayerStatsReader _stats;
        private readonly IScoreReader _scores;
        private readonly IWeeklyTournamentRepository _weeklyTournament;
        private readonly IChartListRepository _chartList;
        private readonly IDateTimeOffsetAccessor _dateTime;
        private readonly IRandomNumberGenerator _random;

        public RecommendedChartsSaga(IMediator mediator, ICurrentUserAccessor currentUser, IFeedbackRepository feedback,
            IPlayerStatsReader stats, IScoreReader scores,
            IWeeklyTournamentRepository weeklyTournament, IChartListRepository chartList,
            IDateTimeOffsetAccessor dateTime, IRandomNumberGenerator random)
        {
            _mediator = mediator;
            _currentUser = currentUser;
            _feedback = feedback;
            _stats = stats;
            _scores = scores;
            _weeklyTournament = weeklyTournament;
            _chartList = chartList;
            _dateTime = dateTime;
            _random = random;
        }

        public async Task<IEnumerable<ChartRecommendation>> Handle(GetRecommendedChartsQuery request,
            CancellationToken cancellationToken)
        {
            // Recommendations follow the caller's mix; a mix without data (Phoenix 2 at
            // launch: empty title list, no scores) naturally yields few or no suggestions.
            var mix = request.Mix;

            // Null = everything (the legacy WhatShouldIPlay page). The Suggested Charts
            // widget requests only its goal bundle's categories, so unrequested builders
            // — and the title fetch that only they need — are skipped entirely.
            bool Include(RecommendationCategory category)
            {
                return request.Categories == null || request.Categories.Contains(category);
            }

            var playerStats = await _stats.GetStats(mix, _currentUser.User.Id, cancellationToken);
            var competitiveLevel = (int)Math.Round(playerStats
                .CompetitiveLevel);
            if (!DifficultyLevel.IsValid(competitiveLevel) || competitiveLevel < 10) competitiveLevel = 10;
            var titles = Include(RecommendationCategory.PushLevel) || Include(RecommendationCategory.SkillTitles)
                ? (await _mediator.Send(new GetTitleProgressQuery(mix), cancellationToken)).ToArray()
                : Array.Empty<TitleProgress>();
            var scores = (await _scores.GetBestScores(mix, _currentUser.User.Id, cancellationToken))
                .ToArray();
            var feedback = (await _feedback.GetFeedback(_currentUser.User.Id, cancellationToken))
                .Where(f => f.ShouldHide)
                .GroupBy(u => u.SuggestionCategory.ToString()).ToDictionary(g => g.Key,
                    g => (ISet<Guid>)g.Select(i => i.ChartId).Distinct().ToHashSet());
            var charts =
                (await _mediator.Send(new GetChartsQuery(mix), cancellationToken)).ToDictionary(c => c.Id);
            var window = request.LevelWindow == null
                ? null
                : await BuildLevelWindow(mix, request.LevelWindow, competitiveLevel, cancellationToken);

            var result = Enumerable.Empty<ChartRecommendation>();
            if (Include(RecommendationCategory.PushLevel))
                result = result.Concat(await GetPushLevels(mix, feedback, cancellationToken, titles, scores,
                    request.ChartType, charts));
            if (Include(RecommendationCategory.WeeklyCharts))
                result = result.Concat(await GetWeeklyCharts(mix, cancellationToken,
                    playerStats.SinglesCompetitiveLevel,
                    playerStats.DoublesCompetitiveLevel, request.LevelOffset, feedback, request.ChartType, charts));
            if (Include(RecommendationCategory.FillScores))
                result = result.Concat(await GetPassFills(mix, feedback, cancellationToken, competitiveLevel, scores,
                    request.ChartType, request.LevelOffset, charts, window));
            if (Include(RecommendationCategory.SkillTitles))
                result = result.Concat(
                    await GetSkillTitleCharts(feedback, cancellationToken, titles, request.ChartType, charts));
            if (Include(RecommendationCategory.RevisitOldScores))
                result = result.Concat(await GetOldScores(mix, cancellationToken, competitiveLevel, scores, feedback,
                    request.ChartType, request.LevelOffset, charts, window));
            if (Include(RecommendationCategory.PushPGs))
                result = result.Concat(
                    await GetPGPushes(feedback, cancellationToken, scores, request.ChartType, charts, window));
            if (Include(RecommendationCategory.ImproveTop50))
                result = result.Concat(
                    await GetRandomFromTop50Charts(mix, feedback, request.ChartType, cancellationToken, charts,
                        window));
            return result.ToArray();
        }

        /// <summary>
        ///     Turns the widget's level config into a chart predicate. Dynamic follows the
        ///     player's (clamped) competitive level; Static is a pinned range. An explicit
        ///     window REPLACES the legacy per-category bands — see the categories it's
        ///     passed to in <see cref="Handle(GetRecommendedChartsQuery,CancellationToken)" />;
        ///     title-driven categories never see it (the pushing title pins its own level).
        /// </summary>
        private async Task<Func<Chart, bool>> BuildLevelWindow(MixEnum mix, RecommendationLevelWindow window,
            int competitiveLevel, CancellationToken cancellationToken)
        {
            var (min, max) = window.Mode == RecommendationLevelMode.Dynamic
                ? (competitiveLevel - window.Spread, competitiveLevel + window.Spread)
                : (window.MinLevel, window.MaxLevel);
            if (window.Basis != RecommendationLevelBasis.ScoringLevel)
                return c => (int)c.Level >= min && (int)c.Level <= max;

            // Scoring level: community-calibrated difficulty — a chart that scores like a
            // 21 counts as a 21. Uncalibrated charts fall back to their printed level.
            var scoringLevels = await _mediator.Send(new GetChartScoringLevelsQuery(mix), cancellationToken);
            return c =>
            {
                var level = scoringLevels.TryGetValue(c.Id, out var scoring)
                    ? (int)Math.Round(scoring)
                    : (int)c.Level;
                return level >= min && level <= max;
            };
        }

        private sealed record OrderedTitle(TitleProgress t, int i)
        {
        }

        private ISet<DifficultyLevel> BuildRange(int min, int max, int offset)
        {
            var result = new List<DifficultyLevel>();
            for (var i = min; i < max; i++)
                if (DifficultyLevel.IsValid(i + offset))
                    result.Add(i + offset);

            return result.ToHashSet();
        }

        private async Task<IEnumerable<ChartRecommendation>> GetWeeklyCharts(MixEnum mix,
            CancellationToken cancellationToken,
            double singlesCompetitive, double doublesCompetitive, int levelOffset,
            IDictionary<string, ISet<Guid>> ignoredChartIds,
            ChartType? chartType, IDictionary<Guid, Chart> charts)
        {
            var skipped = ignoredChartIds.TryGetValue(RecommendationCategories.WeeklyCharts, out var r)
                ? r
                : new HashSet<Guid>();
            var allCharts = await _weeklyTournament.GetWeeklyCharts(mix, cancellationToken);
            var weeklyCharts = WeeklyChartSuggestionPolicy.GetSuggestedCharts(allCharts
                    .Where(c => !skipped.Contains(c.ChartId) && charts.ContainsKey(c.ChartId))
                    .Select(c => charts[c.ChartId]), doublesCompetitive,
                singlesCompetitive).ToArray();
            if (chartType != null)
                weeklyCharts = weeklyCharts.Where(w => w.Type == chartType).Distinct().ToArray();

            return weeklyCharts.OrderByDescending(c => c.Level)
                .Select(c =>
                    new ChartRecommendation(RecommendationCategories.WeeklyCharts, c.Id,
                        "Randomized Rotating Weekly Charts With Leaderboards!"));
        }

        private async Task<IEnumerable<ChartRecommendation>> GetOldScores(MixEnum mix,
            CancellationToken cancellationToken,
            DifficultyLevel competitiveLevel, RecordedPhoenixScore[] scores,
            IDictionary<string, ISet<Guid>> ignoredChartIds, ChartType? chartType, int levelOffset,
            IDictionary<Guid, Chart> charts, Func<Chart, bool>? window)
        {
            // An explicit window replaces the legacy CL−2..CL band. With the scoring-level
            // basis the in-window charts' PRINTED levels drive which relative tier lists to
            // fetch — tier lists are stored per printed level.
            var toFind = window == null
                ? BuildRange(competitiveLevel - 2, competitiveLevel, levelOffset)
                : charts.Values.Where(window).Select(c => (int)c.Level).Distinct()
                    .Where(DifficultyLevel.IsValid).Select(DifficultyLevel.From).ToHashSet();

            var tierLists = new Dictionary<Guid, TierListCategory>();
            foreach (var level in toFind)
            {
                if (chartType is null or ChartType.Single)
                {
                    var mySinglesRating = await _mediator.Send(
                        new GetMyRelativeTierListQuery(ChartType.Single, level, Mix: mix),
                        cancellationToken);
                    foreach (var rating in mySinglesRating) tierLists[rating.ChartId] = rating.Category;
                }

                if (chartType is null or ChartType.Double)
                {
                    var myDoublesRating = await _mediator.Send(
                        new GetMyRelativeTierListQuery(ChartType.Double, level, Mix: mix),
                        cancellationToken);
                    foreach (var rating in myDoublesRating) tierLists[rating.ChartId] = rating.Category;
                }
            }

            var chartIds =
                charts.Values
                    .Where(c => window?.Invoke(c) ?? (c.Level >= competitiveLevel - 2 && c.Level <= competitiveLevel))
                    .Select(c => c.Id)
                    .Distinct()
                    .ToHashSet();

            var cutoff = _dateTime.Now - TimeSpan.FromDays(30);
            var random = _random;
            var now = _dateTime.Now;

            var skipped = ignoredChartIds.TryGetValue(RecommendationCategories.RevisitOldScores, out var r)
                ? r
                : new HashSet<Guid>();
            return scores.Where(r => tierLists.ContainsKey(r.ChartId) &&
                                     tierLists[r.ChartId] is TierListCategory.Underrated or TierListCategory.VeryHard &&
                                     chartIds.Contains(r.ChartId) && !skipped.Contains(r.ChartId) &&
                                     r.RecordedDate <= cutoff)
                .OrderBy(r => r.RecordedDate)
                .Take(30)
                .OrderBy(r => random.Next(100))
                .Take(6)
                .Select(r => new ChartRecommendation(RecommendationCategories.RevisitOldScores, r.ChartId,
                    "Your oldest scores that appear to be needing an update",
                    (now - r.RecordedDate).TotalDays.ToString("0") + " Days Old"));
        }


        private async Task<IEnumerable<ChartRecommendation>> GetRandomFromTop50Charts(MixEnum mix,
            IDictionary<string, ISet<Guid>> ignoredChartIds, ChartType? chartType,
            CancellationToken cancellationToken, IDictionary<Guid, Chart> charts, Func<Chart, bool>? window)
        {
            var skipped = ignoredChartIds.TryGetValue(RecommendationCategories.ImproveTop50, out var r)
                ? r
                : new HashSet<Guid>();
            var random = _random;
            var result = Array.Empty<RecordedPhoenixScore>().AsEnumerable();

            if (chartType is null or ChartType.Single)
                result = result.Concat((await _mediator.Send(
                        new GetTop50CompetitiveQuery(_currentUser.User.Id, ChartType.Single, mix),
                        cancellationToken)).Where(c => c.Score != null && c.Score < 1000000)
                    .Where(c => !skipped.Contains(c.ChartId))
                    .Where(c => window == null || (charts.TryGetValue(c.ChartId, out var chart) && window(chart)))
                    .OrderBy(c => random.Next(int.MaxValue))
                    .Take(chartType == ChartType.Single ? 6 : 3));

            if (chartType is null or ChartType.Double)
                result = result.Concat((await _mediator.Send(
                        new GetTop50CompetitiveQuery(_currentUser.User.Id, ChartType.Double, mix),
                        cancellationToken)).Where(c => c.Score != null && c.Score < 1000000)
                    .Where(c => !skipped.Contains(c.ChartId))
                    .Where(c => window == null || (charts.TryGetValue(c.ChartId, out var chart) && window(chart)))
                    .OrderBy(c => random.Next(int.MaxValue))
                    .Take(chartType == ChartType.Double ? 6 : 3));
            return result
                .Select(c => new ChartRecommendation(RecommendationCategories.ImproveTop50, c.ChartId,
                    "These are randomly pulled from your best 100 charts based on competitive score. Push that score!"));
        }

        private async Task<IEnumerable<ChartRecommendation>> GetSkillTitleCharts(
            IDictionary<string, ISet<Guid>> ignoredChartIds, CancellationToken cancellationToken,
            TitleProgress[] allTitles, ChartType? chartType, IDictionary<Guid, Chart> charts)
        {
            var skipped = ignoredChartIds.TryGetValue(RecommendationCategories.SkillTitles, out var c)
                ? c
                : new HashSet<Guid>();
            return allTitles.Where(t =>
                    t.Title is PhoenixSkillTitle && t.CompletionCount >= PhoenixLetterGrade.S.GetMinimumScore() &&
                    t.CompletionCount < t.Title.CompletionRequired)
                .Select(t => new ChartRecommendation(RecommendationCategories.SkillTitles,
                    charts.Values.First(c => (t.Title as PhoenixSkillTitle)!.AppliesToChart(c)).Id,
                    "Charts you are close to achieving a Skill title (SSS) on"))
                .Where(s => !skipped.Contains(s.ChartId) && (chartType == null || charts[s.ChartId].Type == chartType));
        }

        private async Task<IEnumerable<ChartRecommendation>> GetPGPushes(
            IDictionary<string, ISet<Guid>> ignoredChartIds, CancellationToken cancellationToken,
            RecordedPhoenixScore[] scores, ChartType? chartType, IDictionary<Guid, Chart> charts,
            Func<Chart, bool>? window)
        {
            var skipped = ignoredChartIds.TryGetValue(RecommendationCategories.PushPGs, out var c)
                ? c
                : new HashSet<Guid>();

            return scores.Where(s =>
                    s.Score != null && s.Score != 1000000 && s.Score.Value.LetterGrade == PhoenixLetterGrade.SSSPlus &&
                    (chartType == null || charts[s.ChartId].Type == chartType))
                .Where(s => !skipped.Contains(s.ChartId))
                .Where(s => window == null || (charts.TryGetValue(s.ChartId, out var chart) && window(chart)))
                .OrderByDescending(s => charts[s.ChartId].Level)
                .ThenBy(s => 1000000 - s.Score)
                .Take(6)
                .Select(s =>
                    new ChartRecommendation(RecommendationCategories.PushPGs, s.ChartId,
                        "These are your closest charts to a PG score wise"));
        }

        private async Task<IEnumerable<ChartRecommendation>> GetPassFills(MixEnum mix,
            IDictionary<string, ISet<Guid>> ignoredChartIds, CancellationToken cancellationToken,
            DifficultyLevel competitiveLevel, RecordedPhoenixScore[] scores, ChartType? chartType, int levelOffset,
            IDictionary<Guid, Chart> charts, Func<Chart, bool>? window)
        {
            var skipped = ignoredChartIds.TryGetValue(RecommendationCategories.FillScores, out var c)
                ? c
                : new HashSet<Guid>();

            // Legacy band = CL−3..CL−1; an explicit window replaces it outright.
            var includedLevels = BuildRange(competitiveLevel - 3, competitiveLevel, levelOffset);

            var chartsResults = charts.Values
                .Where(c => window?.Invoke(c) ?? includedLevels.Contains(c.Level))
                .Where(c => chartType == null || c.Type == chartType)
                .ToDictionary(c => c.Id);

            var myScores = scores
                .ToDictionary(s => s.ChartId);
            var random = _random;
            var chartOrder = (await GetApproachableCharts(mix, cancellationToken, charts)).Where(id =>
                    chartsResults.ContainsKey(id)
                    && (!myScores.TryGetValue(id, out var score) || score.IsBroken))
                .GroupBy(c => charts[c].Level)
                .SelectMany(g => g.GroupBy(id => charts[id].Type)
                    .SelectMany(t =>
                        t.Take(chartType == null ? 6 : 12).OrderBy(_ => random.Next(1000))
                            .Where(id => !skipped.Contains(id)).Take(chartType == null ? 2 : 4)));

            return chartOrder.Select(id =>
                new ChartRecommendation(RecommendationCategories.FillScores, id,
                    "Easier Charts From Lower Levels You Can Fill"));
        }

        private async Task<IEnumerable<ChartRecommendation>> GetPushLevels(MixEnum mix,
            IDictionary<string, ISet<Guid>> ignoredChartIds, CancellationToken cancellationToken,
            TitleProgress[] allTitles, RecordedPhoenixScore[] scores, ChartType? chartType,
            IDictionary<Guid, Chart> chartDict)
        {
            var titles = allTitles
                .Where(title => title.Title is PhoenixDifficultyTitle)
                .OrderBy(title => (title.Title as PhoenixDifficultyTitle)!.Level)
                .ThenBy(title => title.Title.CompletionRequired)
                .ToArray();

            // A mix without difficulty titles (Phoenix 2's list is empty at launch) has no
            // push level to recommend toward.
            if (!titles.Any()) return Array.Empty<ChartRecommendation>();

            var firstAchieved = titles.Count() - (titles.Reverse().Select((t, i) => new OrderedTitle(t, i))
                .FirstOrDefault(t => t.t.CompletionCount >= t.t.Title.CompletionRequired)?.i ?? titles.Count());

            var pushLevel = titles[firstAchieved];
            var charts =
                chartDict.Values.Where(c => c.Level == (pushLevel.Title as PhoenixDifficultyTitle)!.Level)
                    .ToDictionary(c => c.Id);
            var myScores = scores
                .ToDictionary(s => s.ChartId);
            var chartOrder = (await GetApproachableCharts(mix, cancellationToken, chartDict))
                .Where(id => charts.ContainsKey(id))
                .ToArray();
            var skippedCharts = ignoredChartIds.TryGetValue($"{pushLevel.Title.Name}", out var cs)
                ? cs
                : new HashSet<Guid>();
            var result = new List<Guid>();
            var random = _random;
            var reduction = chartOrder.Where(c => !myScores.TryGetValue(c, out var score) || score.IsBroken)
                .Where(c => !skippedCharts.Contains(c) && charts.ContainsKey(c))
                .ToArray();
            var missingSingles = 3 - reduction.Where(c => charts[c].Type == ChartType.Single).Take(3).Count();
            var missingDoubles = 3 - reduction.Where(c => charts[c].Type == ChartType.Double).Take(3).Count();
            if (chartType is null or ChartType.Single)
                result.AddRange(reduction
                    .Where(c => charts[c].Type == ChartType.Single)
                    .Take(chartType == ChartType.Single ? 12 : 6)
                    .OrderBy(_ => random.Next(int.MaxValue))
                    .Take(chartType == ChartType.Single ? 6 : 3 + missingDoubles));

            if (chartType is null or ChartType.Double)
                result.AddRange(reduction
                    .Where(c => charts[c].Type == ChartType.Double)
                    .Take(chartType == ChartType.Double ? 12 : 6)
                    .OrderBy(_ => random.Next(int.MaxValue))
                    .Take(chartType == ChartType.Double ? 6 : 3 + missingSingles));
            return result.Select(s => new ChartRecommendation($"{pushLevel.Title.Name}", s,
                "Recommended Charts for achieving your next title"));
        }

        private async Task<IEnumerable<Guid>> GetApproachableCharts(MixEnum mix, CancellationToken cancellationToken,
            IDictionary<Guid, Chart> charts)
        {
            // The Phoenix2 tier-list read path may fall back to Phoenix entries (locked
            // decision), so entries can reference charts absent from this mix's catalog —
            // filter them out instead of throwing.
            var popularity = (await _mediator.Send(new GetTierListQuery("Popularity", mix), cancellationToken))
                .Where(r => charts.ContainsKey(r.ChartId));
            var difficulty = (await _mediator.Send(new GetTierListQuery("Pass Count", mix), cancellationToken))
                .Where(r => charts.ContainsKey(r.ChartId));
            var score = (await _mediator.Send(new GetTierListQuery("Scores", mix), cancellationToken))
                .Where(r => charts.ContainsKey(r.ChartId) && charts[r.ChartId].Level < 20);
            return popularity.Concat(difficulty).Concat(score).GroupBy(s => s.ChartId)
                .OrderByDescending(g => g.Sum(s =>
                    (s.TierListName == "Popularity" ? .5 : 1.0) *
                    s.Category switch

                    {
                        TierListCategory.Overrated => 7.0,
                        TierListCategory.Easy => 6.0,
                        TierListCategory.VeryEasy => 5.0,
                        TierListCategory.Medium => 4.0,
                        TierListCategory.Hard => 3.0,
                        TierListCategory.VeryHard => 2.0,
                        TierListCategory.Underrated => 1.0,
                        _ => 0
                    })).Select(g => g.Key).ToArray();
        }

        public async Task Handle(SubmitFeedbackCommand request, CancellationToken cancellationToken)
        {
            await _feedback.SaveFeedback(_currentUser.User.Id, request.Feedback, cancellationToken);
        }
    }
}
