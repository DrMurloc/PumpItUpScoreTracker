using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The fan-out side of a scoped delete: one request, each vertical removing its own rows in
///     its own consumer. Nothing here knows another vertical exists.
/// </summary>
public sealed class DataDeletionConsumerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static Mock<ConsumeContext<T>> Context<T>(T message) where T : class
    {
        var context = new Mock<ConsumeContext<T>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task ContributionsAreOnlyAskedForWhenSomethingWasChosen()
    {
        var bus = new Mock<IBus>();
        var handler = new DeleteMyContributionsHandler(bus.Object);

        await handler.Handle(new DeleteMyContributionsCommand(UserId, ContributionDeletionItems.None),
            CancellationToken.None);

        bus.Verify(b => b.Publish(It.IsAny<ContributionsDeletionRequestedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChosenContributionsRideOneEventForEveryVertical()
    {
        var bus = new Mock<IBus>();
        var handler = new DeleteMyContributionsHandler(bus.Object);
        var items = ContributionDeletionItems.ChartDifficultyRatings | ContributionDeletionItems.TournamentResults;

        await handler.Handle(new DeleteMyContributionsCommand(UserId, items), CancellationToken.None);

        bus.Verify(b => b.Publish(It.Is<ContributionsDeletionRequestedEvent>(
            e => e.UserId == UserId && e.Items == items), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProgressionStoresRemoveOnlyWhatTheDeleteNamed()
    {
        // Rating history, highlights and milestones are independently choosable; a consumer that
        // took all three on any event would delete data the player kept.
        var data = new Mock<ScoreTracker.PlayerProgress.Domain.IPlayerScoreDataRepository>();
        var consumer = new ScoreTracker.PlayerProgress.Application.PlayerScoreDataDeletedConsumer(data.Object);

        await consumer.Consume(Context(new PlayerScoreDataDeletedEvent(UserId, MixEnum.Phoenix,
            RatingHistory: false, Highlights: true, Milestones: false)).Object);

        data.Verify(d => d.DeleteHighlights(UserId, MixEnum.Phoenix, It.IsAny<CancellationToken>()), Times.Once);
        data.Verify(d => d.DeleteHistory(It.IsAny<Guid>(), It.IsAny<MixEnum?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        data.Verify(d => d.DeleteMilestones(It.IsAny<Guid>(), It.IsAny<MixEnum?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnUndoneSessionTakesItsHighlightsAndMilestones()
    {
        // Neither is recomputed from scores, so an undo that left them behind would keep
        // claiming a title the player no longer holds.
        var data = new Mock<ScoreTracker.PlayerProgress.Domain.IPlayerScoreDataRepository>();
        var consumer = new ScoreTracker.PlayerProgress.Application.ScoreSessionUndoneConsumer(data.Object);
        var sessionId = Guid.NewGuid();

        await consumer.Consume(Context(new ScoreSessionUndoneEvent(UserId, sessionId, MixEnum.Phoenix)).Object);

        data.Verify(d => d.DeleteForSession(UserId, sessionId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
