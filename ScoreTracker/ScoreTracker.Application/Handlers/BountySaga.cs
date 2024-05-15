using MassTransit;
using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Handlers
{
    public sealed class BountySaga(IPhoenixRecordRepository scores, IChartRepository charts,
            IChartBountyRepository bounties, IPlayerStatsRepository playerStats, ICurrentUserAccessor currentUser)
        : IConsumer<UpdateBountiesEvent>,
            IConsumer<PlayerScoreUpdatedEvent>,
            IRequestHandler<GetChartBountiesQuery, IEnumerable<ChartBounty>>
    {
        public async Task Consume(ConsumeContext<UpdateBountiesEvent> context)
        {
            if (DateTimeOffset.Now.Day == 1) await bounties.ClearMonthlyBoard(context.CancellationToken);
            foreach (var level in DifficultyLevel.All)
            foreach (var type in new[] { ChartType.Single, ChartType.Double })
            {
                var includedCharts = await charts.GetCharts(MixEnum.Phoenix, level, type,
                    cancellationToken: context.CancellationToken);

                var recordedScores = (await scores.GetMeaningfulScoresCount(type, level, context.CancellationToken))
                    .ToDictionary(c => c.ChartId, c => c.Count);
                var tierList = TierListSaga.ProcessIntoTierList("Bounties", recordedScores);
                var newBounties = tierList.ToDictionary(t => t.ChartId, t => (int)t.Category + 1);
                foreach (var chart in includedCharts.Where(c => !recordedScores.ContainsKey(c.Id)))
                    newBounties[chart.Id] = 10;

                foreach (var kv in newBounties)
                    await bounties.SetChartBounty(kv.Key, kv.Value, context.CancellationToken);
            }
        }

        public async Task Consume(ConsumeContext<PlayerScoreUpdatedEvent> context)
        {
            var bountyWeights =
                (await GetBounties(context.Message.UserId, context.CancellationToken)).ToDictionary(c => c.ChartId,
                    c => c.Worth);


            var totalAdd = context.Message.NewChartIds.Where(c => bountyWeights.ContainsKey(c))
                .Sum(c => bountyWeights[c]);
            await bounties.RedeemBounty(context.Message.UserId, totalAdd, context.CancellationToken);
        }

        private async Task<IEnumerable<ChartBounty>> GetBounties(Guid userId, CancellationToken cancellationToken)
        {
            var stats = await playerStats.GetStats(userId, cancellationToken);
            var singlesLevel = (int)Math.Floor(stats.SinglesCompetitiveLevel);
            var otherSingles = (int)Math.Round(stats.SinglesCompetitiveLevel);
            if (otherSingles == singlesLevel) otherSingles--;
            var doublesLevel = (int)Math.Floor(stats.DoublesCompetitiveLevel);
            var otherDoubles = (int)Math.Round(stats.DoublesCompetitiveLevel);
            if (otherDoubles == doublesLevel) otherDoubles--;
            var list = new List<ChartBounty>();
            if (DifficultyLevel.IsValid(singlesLevel))
                list.AddRange(await bounties.GetChartBounties(ChartType.Single, singlesLevel, cancellationToken));

            if (DifficultyLevel.IsValid(otherSingles))
                list.AddRange(await bounties.GetChartBounties(ChartType.Single, otherSingles, cancellationToken));

            if (DifficultyLevel.IsValid(doublesLevel))
                list.AddRange(await bounties.GetChartBounties(ChartType.Double, doublesLevel, cancellationToken));
            return list;
        }

        public async Task<IEnumerable<ChartBounty>> Handle(GetChartBountiesQuery request,
            CancellationToken cancellationToken)
        {
            return await GetBounties(currentUser.User.Id, cancellationToken);
        }
    }
}
