using System;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services.UiNotifications;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The host's own consumers, which nothing else covers. The vertical tripwire in
///     <c>AccountPurgeCoverageTests</c> asks the same question of the verticals, but it cannot
///     reach Web — <c>ScoreTracker.Tests</c> is not allowed to reference it.
/// </summary>
public sealed class UiNotificationBridgeTests
{
    [Fact]
    public void TheHostAssemblyScanPicksUpEveryBridgeConsumer()
    {
        // MassTransit's assembly scan skips non-public types, so marking one of these internal —
        // which is what every other class in that file is — unregisters it silently. Nothing
        // fails, no message is logged, and the only symptom is a page that never updates. This
        // repository has shipped that exact bug before, with all five suites green.
        var services = new ServiceCollection();
        services.AddMassTransit(x =>
        {
            // The same assembly Program.cs scans — reached through a type that lives in it.
            x.AddConsumers(typeof(ScoreHighlightsCapturedUiBridge).Assembly);
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });

        var registered = services.Select(d => d.ServiceType).ToHashSet();

        Assert.Contains(typeof(ScoreHighlightsCapturedUiBridge), registered);
    }

    [Fact]
    public async Task CaptureIsBridgedToTheUserWhoseSessionItIs()
    {
        // A per-user topic is the whole privacy story for these bridges: publishing to a shared
        // one would push one player's session into every open circuit.
        var userId = Guid.NewGuid();
        var hub = new Mock<IUiNotificationHub>();
        var context = new Mock<ConsumeContext<ScoreHighlightsCapturedEvent>>();
        context.SetupGet(c => c.Message).Returns(ScoreHighlightsCapturedEvent.Create(
            DateTimeOffset.UtcNow, userId, MixEnum.Phoenix2, Guid.NewGuid(),
            Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
            Array.Empty<PlayerMilestoneRecord>(),
            Array.Empty<TitleProgressDelta>()));

        await new ScoreHighlightsCapturedUiBridge(hub.Object).Consume(context.Object);

        hub.Verify(h => h.PublishAsync(UiTopics.User(userId),
            It.IsAny<ScoreHighlightsCapturedEvent>()), Times.Once);
    }
}
