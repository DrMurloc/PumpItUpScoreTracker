using MassTransit;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class PlayerHistorySaga(IPlayerHistoryRepository history) : IConsumer<PlayerRatingsImprovedEvent>
    {
        public async Task Consume(ConsumeContext<PlayerRatingsImprovedEvent> context)
        {
            await history.WriteHistory(new PlayerRatingRecord(context.Message.UserId, DateTimeOffset.Now,
                context.Message.NewCompetitive,
                context.Message.NewSinglesCompetitive, context.Message.NewDoublesCompetitive,
                context.Message.CoOpRating, context.Message.PassCount), context.CancellationToken);
        }
    }
}
