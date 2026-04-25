using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PlayerHistorySagaTests
{
    [Fact]
    public async Task ConsumePersistsHistoryRecordStampedWithCurrentTime()
    {
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        var history = new Mock<IPlayerHistoryRepository>();

        var saga = new PlayerHistorySaga(history.Object, FakeDateTime.At(now).Object);
        var message = new PlayerRatingsImprovedEvent(userId,
            OldTop50: 0, OldSinglesTop50: 0, OldDoublesTop50: 0,
            NewTop50: 100, NewSinglesTop50: 50, NewDoublesTop50: 60,
            OldCompetitive: 0, NewCompetitive: 17.5,
            OldSinglesCompetitive: 0, NewSinglesCompetitive: 17.0,
            OldDoublesCompetitive: 0, NewDoublesCompetitive: 18.0,
            CoOpRating: 200, PassCount: 42);
        var ctx = new Mock<ConsumeContext<PlayerRatingsImprovedEvent>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await saga.Consume(ctx.Object);

        history.Verify(h => h.WriteHistory(
            It.Is<PlayerRatingRecord>(r => r.UserId == userId
                                            && r.Date == now
                                            && r.CompetitiveLevel == 17.5
                                            && r.SinglesLevel == 17.0
                                            && r.DoublesLevel == 18.0
                                            && r.CoOpRating == 200
                                            && r.PassCount == 42),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
