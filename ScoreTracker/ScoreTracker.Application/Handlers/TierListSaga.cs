using MassTransit;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class TierListSaga : IConsumer<ChartDifficultyUpdatedEvent>
    {
        private readonly IChartDifficultyRatingRepository _chartRatings;
        private readonly IChartRepository _chartRepository;
        private readonly ITierListRepository _tierLists;

        public TierListSaga(IChartDifficultyRatingRepository chartRatings, IChartRepository chartRepository,
            ITierListRepository tierLists)
        {
            _chartRatings = chartRatings;
            _chartRepository = chartRepository;
            _tierLists = tierLists;
        }


        public async Task Consume(ConsumeContext<ChartDifficultyUpdatedEvent> context)
        {
            var cancellationToken = context.CancellationToken;
            var charts = (await _chartRepository.GetCharts(MixEnum.Phoenix, context.Message.Level,
                    context.Message.ChartType, cancellationToken: cancellationToken))
                .ToArray();
            var ratings = (await _chartRatings.GetAllChartRatedDifficulties(MixEnum.Phoenix, cancellationToken))
                .ToDictionary(r => r.ChartId);
            var order = 0;
            foreach (var chart in charts)
            {
                if (!ratings.ContainsKey(chart.Id)) continue;

                var rating = ratings[chart.Id];

                var diff = rating.Difficulty - chart.Level - .5;
                switch (diff)
                {
                    case <= -.75:
                        await _tierLists.SaveEntry(
                            new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Overrated, order),
                            cancellationToken);
                        break;
                    case <= -.375:
                        await _tierLists.SaveEntry(
                            new SongTierListEntry("Difficulty", chart.Id, TierListCategory.VeryEasy, order),
                            cancellationToken);
                        break;
                    case <= -.125:
                        await _tierLists.SaveEntry(
                            new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Easy, order),
                            cancellationToken);
                        break;
                    case < .125:
                        await _tierLists.SaveEntry(
                            new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Medium, order),
                            cancellationToken);
                        break;
                    case < .375:
                        await _tierLists.SaveEntry(
                            new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Hard, order),
                            cancellationToken);
                        break;
                    case < .75:
                        await _tierLists.SaveEntry(
                            new SongTierListEntry("Difficulty", chart.Id, TierListCategory.VeryHard, order),
                            cancellationToken);
                        break;
                    default:
                        await _tierLists.SaveEntry(
                            new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Underrated, order),
                            cancellationToken);
                        break;
                }

                order++;
            }
        }
    }
}
