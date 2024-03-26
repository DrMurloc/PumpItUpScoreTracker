using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PersonalProgress.Queries;

namespace ScoreTracker.Application.Handlers
{
    public sealed class
        GetRecommendedChartsHandler : IRequestHandler<GetRecommendedChartsQuery, IEnumerable<ChartRecommendation>>
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserAccessor _currentUser;

        public GetRecommendedChartsHandler(IMediator mediator, ICurrentUserAccessor currentUser)
        {
            _mediator = mediator;
            _currentUser = currentUser;
        }

        public async Task<IEnumerable<ChartRecommendation>> Handle(GetRecommendedChartsQuery request,
            CancellationToken cancellationToken)
        {
            var titles = (await _mediator.Send(new GetTitleProgressQuery(MixEnum.Phoenix), cancellationToken))
                .ToArray();
            var scores = (await _mediator.Send(new GetPhoenixRecordsQuery(_currentUser.User.Id), cancellationToken))
                .ToArray();

            return (await GetPushLevels(cancellationToken, titles, scores))
                .Concat(await GetSkillTitleCharts(cancellationToken, titles))
                .Concat(await GetRandomFromTop50Charts(cancellationToken))
                .Concat(await GetWeakCharts(cancellationToken, titles, scores)).ToArray();
        }

        private sealed record OrderedTitle(TitleProgress t, int i)
        {
        }

        private async Task<IEnumerable<ChartRecommendation>> GetWeakCharts(CancellationToken cancellationToken,
            TitleProgress[] allTitles, RecordedPhoenixScore[] scores)
        {
            var charts = (
                await _mediator.Send(
                    new GetChartsQuery(MixEnum.Phoenix),
                    cancellationToken)).ToDictionary(c => c.Id);
            if (scores.Length <= 12)
                return scores.Select(s => new ChartRecommendation("Skill Up", s.ChartId,
                    "Charts that are relatively weaker for you compared to other players"));

            var random = new Random();
            var toFind = scores.Where(s => s.Score != null && s.Score < 1000000).OrderBy(s => random.NextInt64(10000))
                .Select(s => charts[s.ChartId].Level)
                .Distinct()
                .Take(3).ToArray();
            var result = new List<ChartRecommendation>();
            foreach (var level in toFind)
            {
                var mySinglesRating = await _mediator.Send(new GetMyRelativeTierListQuery(ChartType.Single, level),
                    cancellationToken);
                var myDoublesRating = await _mediator.Send(new GetMyRelativeTierListQuery(ChartType.Double, level),
                    cancellationToken);
                result.AddRange(mySinglesRating.OrderByDescending(r => r.Order).Take(2).Select(r =>
                    new ChartRecommendation("Skill Up", r.ChartId,
                        "Charts that are relatively weaker for you compared to other players")));

                result.AddRange(myDoublesRating.OrderByDescending(r => r.Order).Take(2).Select(r =>
                    new ChartRecommendation("Skill Up", r.ChartId,
                        "Charts that are relatively weaker for you compared to other players")));
            }

            return result;
        }

        private async Task<IEnumerable<ChartRecommendation>> GetRandomFromTop50Charts(
            CancellationToken cancellationToken)
        {
            var random = new Random();
            var charts =
                await _mediator.Send(new GetTop50ForPlayerQuery(_currentUser.User.Id, null), cancellationToken);
            return charts
                .Where(c => c.Score != null && c.Score < 1000000)
                .OrderBy(c => random.Next())
                .Take(5)
                .Select(c => new ChartRecommendation("Improve Your Top 50", c.ChartId,
                    "These are randomly pulled from your best 50 charts based on rating. Push that score!"));
        }

        private async Task<IEnumerable<ChartRecommendation>> GetSkillTitleCharts(CancellationToken cancellationToken,
            TitleProgress[] allTitles)
        {
            var charts = (await _mediator.Send(new GetChartsQuery(MixEnum.Phoenix), cancellationToken)).ToArray();
            return allTitles.Where(t =>
                    t.Title is PhoenixSkillTitle && t.CompletionCount >= PhoenixLetterGrade.S.GetMinimumScore() &&
                    t.CompletionCount < t.Title.CompletionRequired)
                .Select(t => new ChartRecommendation("Skill Title Charts",
                    charts.First(c => (t.Title as PhoenixSkillTitle)!.MatchesChart(c)).Id,
                    "Charts you are close to achieving a Skill title (SSS) on"));
        }

        private async Task<IEnumerable<ChartRecommendation>> GetPushLevels(CancellationToken cancellationToken,
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
            var nextSingles = chartOrder.Where(c => !myScores.TryGetValue(c, out var score) || score.IsBroken)
                .Where(c => charts.ContainsKey(c) && charts[c].Type == ChartType.Single)
                .Take(5);
            var nextDoubles = chartOrder.Where(c => !myScores.TryGetValue(c, out var score) || score.IsBroken)
                .Where(c => charts.ContainsKey(c) && charts[c].Type == ChartType.Double)
                .Take(5);
            return nextSingles.Select(s => new ChartRecommendation($"{pushLevel.Title.Name} Singles", s,
                    "Recommended Singles charts for achieving your next title"))
                .Concat(nextDoubles.Select(s => new ChartRecommendation($"{pushLevel.Title.Name} Doubles", s,
                    "Recommended Doubles charts for achieving your next title")));
        }

        private async Task<IEnumerable<Guid>> GetApproachableCharts(CancellationToken cancellationToken)
        {
            var popularity = await _mediator.Send(new GetTierListQuery("Popularity"), cancellationToken);
            var difficulty = await _mediator.Send(new GetTierListQuery("Difficulty"), cancellationToken);
            var score = await _mediator.Send(new GetTierListQuery("Official Scores"), cancellationToken);
            return popularity.Concat(difficulty).Concat(score).GroupBy(s => s.ChartId).OrderByDescending(g => g.Sum(s =>
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
    }
}
