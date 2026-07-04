using MassTransit;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.PlayerProgress.Application
{
    internal sealed class PlayerHistorySaga(IPlayerHistoryRepository history, IDateTimeOffsetAccessor dateTime)
        : IConsumer<PlayerRatingsImprovedEvent>
    {
        public async Task Consume(ConsumeContext<PlayerRatingsImprovedEvent> context)
        {
            await history.WriteHistory(context.Message.Mix, new PlayerRatingRecord(context.Message.UserId,
                dateTime.Now,
                context.Message.NewCompetitive,
                context.Message.NewSinglesCompetitive, context.Message.NewDoublesCompetitive,
                context.Message.CoOpRating, context.Message.PassCount), context.CancellationToken);
        }
    }
}
