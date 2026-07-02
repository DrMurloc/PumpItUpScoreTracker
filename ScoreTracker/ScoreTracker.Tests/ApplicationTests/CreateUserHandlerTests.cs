using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Events;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.Application.Commands;
using ScoreTracker.Identity.Application;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class CreateUserHandlerTests
{
    [Fact]
    public async Task PersistsUserAndPublishesUserCreatedEvent()
    {
        var users = new Mock<IUserRepository>();
        var bus = new Mock<IBus>();
        var handler = new CreateUserHandler(users.Object, bus.Object);

        var name = Name.From("test-user");

        var result = await handler.Handle(new CreateUserCommand(name), CancellationToken.None);

        Assert.Equal(name, result.Name);
        Assert.NotEqual(Guid.Empty, result.Id);

        users.Verify(
            u => u.SaveUser(It.Is<User>(saved => saved.Id == result.Id && saved.Name == name),
                It.IsAny<CancellationToken>()),
            Times.Once);

        bus.Verify(
            b => b.Publish(It.Is<UserCreatedEvent>(e => e.UserId == result.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
