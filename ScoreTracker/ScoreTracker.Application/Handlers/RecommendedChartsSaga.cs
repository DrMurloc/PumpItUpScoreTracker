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

        public RecommendedChartsSaga(IMediator mediator, ICurrentUserAccessor currentUser, IUserRepository users,
            IPlayerStatsRepository stats)
        {
            _mediator = mediator;
            _currentUser = currentUser;
            _users = users;
            _stats = stats;
        }

        public async Task<IEnumerable<ChartRecommendation>> Handle(GetRecommendedChartsQuery request,
            CancellationToken cancellationToken)
        {
            var competitiveLevel =
                DifficultyLevel.From((int)Math.Round((await _stats.GetStats(_currentUser.User.Id, cancellationToken))
                    .CompetitiveLevel));
            if (competitiveLevel < 2) competitiveLevel = 10;
            var titles = (await _mediator.Send(new GetTitleProgressQuery(MixEnum.Phoenix), cancellationToken))
                .ToArray();
            var scores = (await _mediator.Send(new GetPhoenixRecordsQuery(_currentUser.User.Id), cancellationToken))
                .ToArray();
            var feedback = (await _users.GetFeedback(_currentUser.User.Id, cancellationToken))
                .Where(f => f.ShouldHide)
                .GroupBy(u => u.SuggestionCategory.ToString()).ToDictionary(g => g.Key,
                    g => (ISet<Guid>)g.Select(i => i.ChartId).Distinct().ToHashSet());
            return (await GetPushLevels(feedback, cancellationToken, titles, scores))
                .Concat(await GetPassFills(feedback, cancellationToken, competitiveLevel, scores))
                .Concat(await GetSkillTitleCharts(feedback, cancellationToken, titles))
                .Concat(await GetPGPushes(feedback, cancellationToken, scores))
                .Concat(await GetRandomFromTop50Charts(feedback, cancellationToken))
                .Concat(await GetWeakCharts(cancellationToken, competitiveLevel, scores, feedback)).ToArray();
        }

        private sealed record OrderedTitle(TitleProgress t, int i)
        {
        }

        private async Task<IEnumerable<ChartRecommendation>> GetWeakCharts(CancellationToken cancellationToken,
            DifficultyLevel competitiveLevel, RecordedPhoenixScore[] scores,
            IDictionary<string, ISet<Guid>> ignoredChartIds)
        {
            if (scores.Length <= 12)
                return scores.Where(s => s.Score != null && s.Score < 1000000).Select(s =>
                    new ChartRecommendation("Skill Up", s.ChartId,
                        "Charts that are relatively weaker for you compared to other players"));

            var skipped = ignoredChartIds.TryGetValue("Skill Up", out var charts) ? charts : new HashSet<Guid>();


            var titleLevel = competitiveLevel;
            int[] toFind = { titleLevel, titleLevel - 1, titleLevel - 2, titleLevel - 3 };
            var random = new Random();

            var result = new List<ChartRecommendation>();
            foreach (var level in toFind)
            {
                var mySinglesRating = await _mediator.Send(new GetMyRelativeTierListQuery(ChartType.Single, level),
                    cancellationToken);
                var myDoublesRating = await _mediator.Send(new GetMyRelativeTierListQuery(ChartType.Double, level),
                    cancellationToken);
                result.AddRange(mySinglesRating.OrderByDescending(r => r.Order)
                    .Where(s => !skipped.Contains(s.ChartId))
                    .Take(6)
                    .OrderBy(_ => random.NextInt64(10000))
                    .Take(2).Select(r =>
                        new ChartRecommendation("Skill Up", r.ChartId,
                            "Charts that are relatively weaker for you compared to other players")));

                result.AddRange(myDoublesRating.OrderByDescending(r => r.Order)
                    .Where(s => !skipped.Contains(s.ChartId))
                    .Take(6)
                    .OrderBy(_ => random.NextInt64(10000)).Take(2).Select(r =>
                        new ChartRecommendation("Skill Up", r.ChartId,
                            "Charts that are relatively weaker for you compared to other players")));
            }

            return result;
        }

        private async Task<IEnumerable<ChartRecommendation>> GetRandomFromTop50Charts(
            IDictionary<string, ISet<Guid>> ignoredChartIds,
            CancellationToken cancellationToken)
        {
            var skipped = ignoredChartIds.TryGetValue("Improve Your Top 100", out var r) ? r : new HashSet<Guid>();
            var random = new Random();
            var charts =
                await _mediator.Send(new GetTop50CompetitiveQuery(_currentUser.User.Id, null), cancellationToken);
            return charts
                .Where(c => c.Score != null && c.Score < 1000000)
                .Where(c => !skipped.Contains(c.ChartId))
                .OrderBy(c => random.Next())
                .Take(6)
                .Select(c => new ChartRecommendation("Improve Your Top 50", c.ChartId,
                    "These are randomly pulled from your best 100 charts based on competitive score. Push that score!"));
        }

        private async Task<IEnumerable<ChartRecommendation>> GetSkillTitleCharts(
            IDictionary<string, ISet<Guid>> ignoredChartIds, CancellationToken cancellationToken,
            TitleProgress[] allTitles)
        {
            var skipped = ignoredChartIds.TryGetValue("Skill Title Charts", out var c) ? c : new HashSet<Guid>();
            var charts = (await _mediator.Send(new GetChartsQuery(MixEnum.Phoenix), cancellationToken)).ToArray();
            return allTitles.Where(t =>
                    t.Title is PhoenixSkillTitle && t.CompletionCount >= PhoenixLetterGrade.S.GetMinimumScore() &&
                    t.CompletionCount < t.Title.CompletionRequired)
                .Select(t => new ChartRecommendation("Skill Title Charts",
                    charts.First(c => (t.Title as PhoenixSkillTitle)!.MatchesChart(c)).Id,
                    "Charts you are close to achieving a Skill title (SSS) on"))
                .Where(s => !skipped.Contains(s.ChartId));
        }

        private async Task<IEnumerable<ChartRecommendation>> GetPGPushes(
            IDictionary<string, ISet<Guid>> ignoredChartIds, CancellationToken cancellationToken,
            RecordedPhoenixScore[] scores)
        {
            var skipped = ignoredChartIds.TryGetValue("Push PGs", out var c) ? c : new HashSet<Guid>();
            var charts =
                (await _mediator.Send(new GetChartsQuery(MixEnum.Phoenix), cancellationToken)).ToDictionary(c => c.Id);
            return scores.Where(s =>
                    s.Score != null && s.Score != 1000000 && s.Score.Value.LetterGrade == PhoenixLetterGrade.SSSPlus)
                .Where(s => !skipped.Contains(s.ChartId))
                .OrderByDescending(s => charts[s.ChartId].Level)
                .ThenBy(s => 1000000 - s.Score)
                .Take(6)
                .Select(s =>
                    new ChartRecommendation("Push PGs", s.ChartId, "These are your closest charts to a PG score wise"));
        }

        private async Task<IEnumerable<ChartRecommendation>> GetPassFills(
            IDictionary<string, ISet<Guid>> ignoredChartIds, CancellationToken cancellationToken,
            DifficultyLevel competitiveLevel, RecordedPhoenixScore[] scores)
        {
            var skipped = ignoredChartIds.TryGetValue("Fill Scores", out var c) ? c : new HashSet<Guid>();

            var pushLevel = competitiveLevel;

            var charts = (
                    await _mediator.Send(
                        new GetChartsQuery(MixEnum.Phoenix),
                        cancellationToken))
                .Where(c => c.Level >= pushLevel - 3 && c.Level <= pushLevel).ToDictionary(c => c.Id);

            var myScores = scores
                .ToDictionary(s => s.ChartId);
            var random = new Random();
            var chartOrder = (await GetApproachableCharts(cancellationToken)).Where(id => charts.ContainsKey(id)
                    && (!myScores.TryGetValue(id, out var score) || score.IsBroken))
                .GroupBy(c => charts[c].Level)
                .SelectMany(g => g.GroupBy(id => charts[id].Type)
                    .SelectMany(t =>
                        t.Take(6).OrderBy(_ => random.Next(1000)).Where(id => !skipped.Contains(id)).Take(2)));

            return chartOrder.Select(id =>
                new ChartRecommendation("Fill Scores", id, "Easier Charts From Lower Levels You Can Fill"));
        }

        private async Task<IEnumerable<ChartRecommendation>> GetPushLevels(
            IDictionary<string, ISet<Guid>> ignoredChartIds, CancellationToken cancellationToken,
            TitleProgress[] allTitles, RecordedPhoenixScore[] scores)
        {
            var titles = allTitles
                .Where(title => title.Title is PhoenixDifficultyTitle)
                .OrderBy(title => (title.Title as PhoenixDifficultyTitle)!.Level)
                .ThenBy(title => title.Title.Name)
                .ToArray();

            var firstAchieved = titles.Count() - (titles.Reverse().Select((t, i) => new OrderedTitle(t, i))
                .FirstOrDefault(t => t.t.CompletionCount >= t.t.Title.CompletionRequired)?.i ?? titles.Count());

            var pushLevel = titles[firstAchieved];
            var charts = (
                await _mediator.Send(
                    new GetChartsQuery(MixEnum.Phoenix, (pushLevel.Title as PhoenixDifficultyTitle)!.Level),
                    cancellationToken)).ToDictionary(c => c.Id);
            var myScores = scores
                .ToDictionary(s => s.ChartId);
            var chartOrder = (await GetApproachableCharts(cancellationToken)).Where(id => charts.ContainsKey(id))
                .ToArray();
            var skippedSingles = ignoredChartIds.TryGetValue($"{pushLevel.Title.Name} Singles", out var cs)
                ? cs
                : new HashSet<Guid>();
            var skippedDoubles = ignoredChartIds.TryGetValue($"{pushLevel.Title.Name} Doubles", out var cd)
                ? cd
                : new HashSet<Guid>();
            var nextSingles = chartOrder.Where(c => !myScores.TryGetValue(c, out var score) || score.IsBroken)
                .Where(c => !skippedSingles.Contains(c))
                .Where(c => charts.ContainsKey(c) && charts[c].Type == ChartType.Single)
                .Take(6);
            var nextDoubles = chartOrder.Where(c => !myScores.TryGetValue(c, out var score) || score.IsBroken)
                .Where(c => !skippedDoubles.Contains(c))
                .Where(c => charts.ContainsKey(c) && charts[c].Type == ChartType.Double)
                .Take(6);

            return nextSingles.Select(s => new ChartRecommendation($"{pushLevel.Title.Name} Singles", s,
                    "Recommended Singles charts for achieving your next title"))
                .Concat(nextDoubles.Select(s => new ChartRecommendation($"{pushLevel.Title.Name} Doubles", s,
                    "Recommended Doubles charts for achieving your next title")));
        }

        private async Task<IEnumerable<Guid>> GetApproachableCharts(CancellationToken cancellationToken)
        {
            var chartLevels =
                (await _mediator.Send(new GetChartsQuery(MixEnum.Phoenix), cancellationToken)).ToDictionary(c => c.Id,
                    c => c.Level);

            var popularity = await _mediator.Send(new GetTierListQuery("Popularity"), cancellationToken);
            var difficulty = await _mediator.Send(new GetTierListQuery("Difficulty"), cancellationToken);
            var officialScore =
                (await _mediator.Send(new GetTierListQuery("Official Scores"), cancellationToken)).Where(r =>
                    chartLevels[r.ChartId] >= 20);
            var score = (await _mediator.Send(new GetTierListQuery("Scores"), cancellationToken))
                .Where(r => chartLevels[r.ChartId] < 20);
            return popularity.Concat(difficulty).Concat(score).Concat(officialScore).GroupBy(s => s.ChartId)
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
