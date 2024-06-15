using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.PersonalProgress.Queries;

namespace ScoreTracker.Application.Handlers
{
    public sealed class
        RecommendedChartsSaga : IRequestHandler<GetRecommendedChartsQuery, IEnumerable<ChartRecommendation>>,
            IRequestHandler<SubmitFeedbackCommand>
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IUserRepository _users;
        private readonly IPlayerStatsRepository _stats;
        private readonly IPhoenixRecordRepository _scores;
        private readonly IChartBountyRepository _bounties;

        public RecommendedChartsSaga(IMediator mediator, ICurrentUserAccessor currentUser, IUserRepository users,
            IPlayerStatsRepository stats, IPhoenixRecordRepository scores, IChartBountyRepository bounties)
        {
            _mediator = mediator;
            _currentUser = currentUser;
            _users = users;
            _stats = stats;
            _scores = scores;
            _bounties = bounties;
        }

        public async Task<IEnumerable<ChartRecommendation>> Handle(GetRecommendedChartsQuery request,
            CancellationToken cancellationToken)
        {
            var competitiveLevel = (int)Math.Round((await _stats.GetStats(_currentUser.User.Id, cancellationToken))
                .CompetitiveLevel);
            if (!DifficultyLevel.IsValid(competitiveLevel) || competitiveLevel < 10) competitiveLevel = 10;

            var titles = (await _mediator.Send(new GetTitleProgressQuery(MixEnum.Phoenix), cancellationToken))
                .ToArray();
            var scores = (await _mediator.Send(new GetPhoenixRecordsQuery(_currentUser.User.Id), cancellationToken))
                .ToArray();
            var feedback = (await _users.GetFeedback(_currentUser.User.Id, cancellationToken))
                .Where(f => f.ShouldHide)
                .GroupBy(u => u.SuggestionCategory.ToString()).ToDictionary(g => g.Key,
                    g => (ISet<Guid>)g.Select(i => i.ChartId).Distinct().ToHashSet());
            var charts =
                (await _mediator.Send(new GetChartsQuery(MixEnum.Phoenix), cancellationToken)).ToDictionary(c => c.Id);
            return (await GetPushLevels(feedback, cancellationToken, titles, scores, request.ChartType, charts))
                .Concat(await GetPassFills(feedback, cancellationToken, competitiveLevel, scores, request.ChartType,
                    request.LevelOffset, charts))
                .Concat(await GetSkillTitleCharts(feedback, cancellationToken, titles, request.ChartType, charts))
                .Concat(await GetOldScores(cancellationToken, competitiveLevel, scores, feedback, request.ChartType,
                    request.LevelOffset, charts))
                .Concat(await GetPGPushes(feedback, cancellationToken, scores, request.ChartType, charts))
                .Concat(await GetRandomFromTop50Charts(feedback, request.ChartType, cancellationToken))
                .Concat(await GetBounties(feedback, cancellationToken, scores, request.ChartType, charts)).ToArray();
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

        private async Task<IEnumerable<ChartRecommendation>> GetOldScores(CancellationToken cancellationToken,
            DifficultyLevel competitiveLevel, RecordedPhoenixScore[] scores,
            IDictionary<string, ISet<Guid>> ignoredChartIds, ChartType? chartType, int levelOffset,
            IDictionary<Guid, Chart> charts)
        {
            var toFind = BuildRange(competitiveLevel - 2, competitiveLevel, levelOffset);

            var tierLists = new Dictionary<Guid, TierListCategory>();
            foreach (var level in toFind)
            {
                if (chartType is null or ChartType.Single)
                {
                    var mySinglesRating = await _mediator.Send(new GetMyRelativeTierListQuery(ChartType.Single, level),
                        cancellationToken);
                    foreach (var rating in mySinglesRating) tierLists[rating.ChartId] = rating.Category;
                }

                if (chartType is null or ChartType.Double)
                {
                    var myDoublesRating = await _mediator.Send(new GetMyRelativeTierListQuery(ChartType.Double, level),
                        cancellationToken);
                    foreach (var rating in myDoublesRating) tierLists[rating.ChartId] = rating.Category;
                }
            }

            var chartIds =
                charts.Values
                    .Where(c => c.Level >= competitiveLevel - 2 && c.Level <= competitiveLevel).Select(c => c.Id)
                    .Distinct()
                    .ToHashSet();

            var cutoff = DateTimeOffset.Now - TimeSpan.FromDays(30);
            var random = new Random();
            var now = DateTimeOffset.Now;

            var skipped = ignoredChartIds.TryGetValue("Revisit Old Scores", out var r) ? r : new HashSet<Guid>();
            return scores.Where(r => tierLists.ContainsKey(r.ChartId) &&
                                     tierLists[r.ChartId] is TierListCategory.Underrated or TierListCategory.VeryHard &&
                                     chartIds.Contains(r.ChartId) && !skipped.Contains(r.ChartId) &&
                                     r.RecordedDate <= cutoff)
                .OrderBy(r => r.RecordedDate)
                .Take(30)
                .OrderBy(r => random.Next(100))
                .Take(6)
                .Select(r => new ChartRecommendation("Revisit Old Scores", r.ChartId,
                    "Your oldest scores that appear to be needing an update",
                    (now - r.RecordedDate).TotalDays.ToString("0") + " Days Old"));
        }


        private async Task<IEnumerable<ChartRecommendation>> GetRandomFromTop50Charts(
            IDictionary<string, ISet<Guid>> ignoredChartIds, ChartType? chartType,
            CancellationToken cancellationToken)
        {
            var skipped = ignoredChartIds.TryGetValue("Improve Your Top 50", out var r) ? r : new HashSet<Guid>();
            var random = new Random();
            var result = Array.Empty<RecordedPhoenixScore>().AsEnumerable();

            if (chartType is null or ChartType.Single)
                result = result.Concat((await _mediator.Send(
                        new GetTop50CompetitiveQuery(_currentUser.User.Id, ChartType.Single),
                        cancellationToken)).Where(c => c.Score != null && c.Score < 1000000)
                    .Where(c => !skipped.Contains(c.ChartId))
                    .OrderBy(c => random.Next())
                    .Take(chartType == ChartType.Single ? 6 : 3));

            if (chartType is null or ChartType.Double)
                result = result.Concat((await _mediator.Send(
                        new GetTop50CompetitiveQuery(_currentUser.User.Id, ChartType.Double),
                        cancellationToken)).Where(c => c.Score != null && c.Score < 1000000)
                    .Where(c => !skipped.Contains(c.ChartId))
                    .OrderBy(c => random.Next())
                    .Take(chartType == ChartType.Double ? 6 : 3));
            return result
                .Select(c => new ChartRecommendation("Improve Your Top 50", c.ChartId,
                    "These are randomly pulled from your best 100 charts based on competitive score. Push that score!"));
        }

        private async Task<IEnumerable<ChartRecommendation>> GetSkillTitleCharts(
            IDictionary<string, ISet<Guid>> ignoredChartIds, CancellationToken cancellationToken,
            TitleProgress[] allTitles, ChartType? chartType, IDictionary<Guid, Chart> charts)
        {
            var skipped = ignoredChartIds.TryGetValue("Skill Title Charts", out var c) ? c : new HashSet<Guid>();
            return allTitles.Where(t =>
                    t.Title is PhoenixSkillTitle && t.CompletionCount >= PhoenixLetterGrade.S.GetMinimumScore() &&
                    t.CompletionCount < t.Title.CompletionRequired)
                .Select(t => new ChartRecommendation("Skill Title Charts",
                    charts.Values.First(c => (t.Title as PhoenixSkillTitle)!.AppliesToChart(c)).Id,
                    "Charts you are close to achieving a Skill title (SSS) on"))
                .Where(s => !skipped.Contains(s.ChartId) && (chartType == null || charts[s.ChartId].Type == chartType));
        }

        private async Task<IEnumerable<ChartRecommendation>> GetPGPushes(
            IDictionary<string, ISet<Guid>> ignoredChartIds, CancellationToken cancellationToken,
            RecordedPhoenixScore[] scores, ChartType? chartType, IDictionary<Guid, Chart> charts)
        {
            var skipped = ignoredChartIds.TryGetValue("Push PGs", out var c) ? c : new HashSet<Guid>();

            return scores.Where(s =>
                    s.Score != null && s.Score != 1000000 && s.Score.Value.LetterGrade == PhoenixLetterGrade.SSSPlus &&
                    (chartType == null || charts[s.ChartId].Type == chartType))
                .Where(s => !skipped.Contains(s.ChartId))
                .OrderByDescending(s => charts[s.ChartId].Level)
                .ThenBy(s => 1000000 - s.Score)
                .Take(6)
                .Select(s =>
                    new ChartRecommendation("Push PGs", s.ChartId, "These are your closest charts to a PG score wise"));
        }

        private async Task<IEnumerable<ChartRecommendation>> GetPassFills(
            IDictionary<string, ISet<Guid>> ignoredChartIds, CancellationToken cancellationToken,
            DifficultyLevel competitiveLevel, RecordedPhoenixScore[] scores, ChartType? chartType, int levelOffset,
            IDictionary<Guid, Chart> charts)
        {
            var skipped = ignoredChartIds.TryGetValue("Fill Scores", out var c) ? c : new HashSet<Guid>();

            var includedLevels = BuildRange(competitiveLevel - 3, competitiveLevel, levelOffset);

            var chartsResults = charts.Values
                .Where(c => includedLevels.Contains(c.Level) && (chartType == null || c.Type == chartType))
                .ToDictionary(c => c.Id);

            var myScores = scores
                .ToDictionary(s => s.ChartId);
            var random = new Random();
            var chartOrder = (await GetApproachableCharts(cancellationToken, charts)).Where(id =>
                    chartsResults.ContainsKey(id)
                    && (!myScores.TryGetValue(id, out var score) || score.IsBroken))
                .GroupBy(c => charts[c].Level)
                .SelectMany(g => g.GroupBy(id => charts[id].Type)
                    .SelectMany(t =>
                        t.Take(chartType == null ? 6 : 12).OrderBy(_ => random.Next(1000))
                            .Where(id => !skipped.Contains(id)).Take(chartType == null ? 2 : 4)));

            return chartOrder.Select(id =>
                new ChartRecommendation("Fill Scores", id, "Easier Charts From Lower Levels You Can Fill"));
        }

        private async Task<IEnumerable<ChartRecommendation>> GetBounties(
            IDictionary<string, ISet<Guid>> ignoredChartIds, CancellationToken cancellationToken,
            RecordedPhoenixScore[] scores, ChartType? chartType, IDictionary<Guid, Chart> charts)
        {
            var random = new Random();
            var ignoredCharts = ignoredChartIds.TryGetValue("Bounties", out var set) ? set : new HashSet<Guid>();
            var existingScores = scores.Where(s => s.Score != null).Select(s => s.ChartId).Distinct().ToHashSet();
            return (await _mediator.Send(new GetChartBountiesQuery(), cancellationToken)).Where(b =>
                    chartType == null || charts[b.ChartId].Type == chartType)
                .OrderByDescending(b => b.Worth)
                .Take(20)
                .OrderBy(r => random.Next())
                .Take(5)
                .Select(b => new ChartRecommendation("Bounties", b.ChartId,
                    "Charts that have low data present on the site", $"{b.Worth} Points"));
        }

        private async Task<IEnumerable<ChartRecommendation>> GetPushLevels(
            IDictionary<string, ISet<Guid>> ignoredChartIds, CancellationToken cancellationToken,
            TitleProgress[] allTitles, RecordedPhoenixScore[] scores, ChartType? chartType,
            IDictionary<Guid, Chart> chartDict)
        {
            var titles = allTitles
                .Where(title => title.Title is PhoenixDifficultyTitle)
                .OrderBy(title => (title.Title as PhoenixDifficultyTitle)!.Level)
                .ThenBy(title => title.Title.CompletionRequired)
                .ToArray();

            var firstAchieved = titles.Count() - (titles.Reverse().Select((t, i) => new OrderedTitle(t, i))
                .FirstOrDefault(t => t.t.CompletionCount >= t.t.Title.CompletionRequired)?.i ?? titles.Count());

            var pushLevel = titles[firstAchieved];
            var charts =
                chartDict.Values.Where(c => c.Level == (pushLevel.Title as PhoenixDifficultyTitle)!.Level)
                    .ToDictionary(c => c.Id);
            var myScores = scores
                .ToDictionary(s => s.ChartId);
            var chartOrder = (await GetApproachableCharts(cancellationToken, chartDict))
                .Where(id => charts.ContainsKey(id))
                .ToArray();
            var skippedCharts = ignoredChartIds.TryGetValue($"{pushLevel.Title.Name}", out var cs)
                ? cs
                : new HashSet<Guid>();
            var result = new List<Guid>();
            var random = new Random();
            var reduction = chartOrder.Where(c => !myScores.TryGetValue(c, out var score) || score.IsBroken)
                .Where(c => !skippedCharts.Contains(c) && charts.ContainsKey(c))
                .ToArray();
            var missingSingles = 3 - reduction.Where(c => charts[c].Type == ChartType.Single).Take(3).Count();
            var missingDoubles = 3 - reduction.Where(c => charts[c].Type == ChartType.Double).Take(3).Count();
            if (chartType is null or ChartType.Single)
                result.AddRange(reduction
                    .Where(c => charts[c].Type == ChartType.Single)
                    .Take(chartType == ChartType.Single ? 12 : 6)
                    .OrderBy(_ => random.Next())
                    .Take(chartType == ChartType.Single ? 6 : 3 + missingDoubles));

            if (chartType is null or ChartType.Double)
                result.AddRange(reduction
                    .Where(c => charts[c].Type == ChartType.Double)
                    .Take(chartType == ChartType.Double ? 12 : 6)
                    .OrderBy(_ => random.Next())
                    .Take(chartType == ChartType.Double ? 6 : 3 + missingSingles));
            return result.Select(s => new ChartRecommendation($"{pushLevel.Title.Name}", s,
                "Recommended Charts for achieving your next title"));
        }

        private async Task<IEnumerable<Guid>> GetApproachableCharts(CancellationToken cancellationToken,
            IDictionary<Guid, Chart> charts)
        {
            var popularity = await _mediator.Send(new GetTierListQuery("Popularity"), cancellationToken);
            var difficulty = await _mediator.Send(new GetTierListQuery("Pass Count"), cancellationToken);
            var score = (await _mediator.Send(new GetTierListQuery("Scores"), cancellationToken))
                .Where(r => charts[r.ChartId].Level < 20);
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
            await _users.SaveFeedback(_currentUser.User.Id, request.Feedback, cancellationToken);
        }
    }
}
