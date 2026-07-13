using System;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Moq;
using ScoreTracker.Domain.Events;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class RunOfficialImportConsumerTests
{
    private static ConsumeContext<RunOfficialImportCommand> Context(RunOfficialImportCommand message)
    {
        var context = new Mock<ConsumeContext<RunOfficialImportCommand>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }

    [Fact]
    public async Task RunsTheImportForTheMessagesUserAndSid()
    {
        var mediator = new Mock<IMediator>();
        var consumer = new RunOfficialImportConsumer(mediator.Object);
        var userId = Guid.NewGuid();

        await consumer.Consume(Context(new RunOfficialImportCommand(userId, MixEnum.Phoenix, "sid123", "card1",
            "TAG", true, false)));

        mediator.Verify(m => m.Send(It.Is<ExecuteImportCommand>(c =>
                c.UserId == userId && c.Mix == MixEnum.Phoenix && c.Sid == "sid123" && c.CardId == "card1" &&
                c.IncludeBroken),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidCredentialAtTheSiteSurfacesAsAStatusError()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidCredentialException("bad"));
        var consumer = new RunOfficialImportConsumer(mediator.Object);
        var userId = Guid.NewGuid();

        await consumer.Consume(Context(new RunOfficialImportCommand(userId, MixEnum.Phoenix, "sid123", "card1",
            "TAG", false, false)));

        mediator.Verify(m => m.Publish(It.Is<ImportStatusErrorEvent>(e =>
                e.UserId == userId && e.Error == "Invalid Login Information"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
