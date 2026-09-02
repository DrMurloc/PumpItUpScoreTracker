using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts.Messages;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class DiscordGatewayWatchdogSagaTests
{
    private readonly Mock<IBotClient> _bot = new();

    private DiscordGatewayWatchdogSaga Saga()
    {
        return new DiscordGatewayWatchdogSaga(_bot.Object, NullLogger<DiscordGatewayWatchdogSaga>.Instance);
    }

    private static ConsumeContext<CheckDiscordGatewayCommand> Context()
    {
        var ctx = new Mock<ConsumeContext<CheckDiscordGatewayCommand>>();
        ctx.SetupGet(c => c.Message).Returns(new CheckDiscordGatewayCommand());
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    [Fact]
    public async Task AConnectedGatewayIsLeftAlone()
    {
        _bot.SetupGet(b => b.Status).Returns(BotGatewayStatus.Connected);

        await Saga().Consume(Context());

        _bot.Verify(b => b.Restart(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ABriefDisconnectIsLeftAlone()
    {
        // Normal resumes take a second or two; a restart there would only add a gap.
        _bot.SetupGet(b => b.Status).Returns(BotGatewayStatus.DisconnectedSince(TimeSpan.FromMinutes(2)));

        await Saga().Consume(Context());

        _bot.Verify(b => b.Restart(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JustUnderTheThresholdIsStillLeftAlone()
    {
        _bot.SetupGet(b => b.Status).Returns(
            BotGatewayStatus.DisconnectedSince(DiscordGatewayWatchdogSaga.RestartAfter - TimeSpan.FromSeconds(1)));

        await Saga().Consume(Context());

        _bot.Verify(b => b.Restart(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ALongDisconnectRestartsTheClientOnce()
    {
        _bot.SetupGet(b => b.Status).Returns(
            BotGatewayStatus.DisconnectedSince(DiscordGatewayWatchdogSaga.RestartAfter));

        await Saga().Consume(Context());

        _bot.Verify(b => b.Restart(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ABotThatWasNeverStartedIsLeftAlone()
    {
        // Local dev and E2E run without a token; there is nothing to restart.
        _bot.SetupGet(b => b.Status).Returns(BotGatewayStatus.NotStarted);

        await Saga().Consume(Context());

        _bot.Verify(b => b.Restart(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AFailedRestartIsSwallowedSoTheNextTickRetries()
    {
        _bot.SetupGet(b => b.Status).Returns(BotGatewayStatus.DisconnectedSince(TimeSpan.FromHours(1)));
        _bot.Setup(b => b.Restart(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Discord is down"));

        await Saga().Consume(Context());

        _bot.Verify(b => b.Restart(It.IsAny<CancellationToken>()), Times.Once);
    }
}
