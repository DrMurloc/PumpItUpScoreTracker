using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using ScoreTracker.CommunityTools.Application;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure;
using ScoreTracker.CommunityTools.Wiring;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     Where a webhook may be sent from THIS instance. A local run is a copy of production, with
///     real tools, real endpoints and deliveries still queued, so it keeps its webhooks on the
///     machine: public targets are refused at the one client every POST goes through, before a byte
///     leaves, and loopback still delivers for the maker developing locally.
/// </summary>
public sealed class WebhookTargetPolicyTests
{
    /// <summary>Answers 200 and remembers every request, so a refusal can be proven by absence.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<Uri> Sent { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Sent.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty)
            });
        }
    }

    private static (WebhookDeliveryClient Client, RecordingHandler Wire) Client(bool allowPublicTargets)
    {
        var wire = new RecordingHandler();
        var client = new WebhookDeliveryClient(new HttpClient(wire),
            Options.Create(new CommunityToolsConfiguration { AllowPublicWebhookTargets = allowPublicTargets }));
        return (client, wire);
    }

    private static readonly Uri PublicHook = new("https://tool.example/hooks/piuscores");

    [Fact]
    public async Task ALocalRunRefusesAPublicTargetBeforeAnyByteLeaves()
    {
        var (client, wire) = Client(allowPublicTargets: false);

        var outcome = await client.Post(PublicHook, "{}", "d-1", "X-Token", "secret", CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Equal(WebhookFailureReason.RefusedTarget, outcome.Reason);
        Assert.False(outcome.Retryable);
        Assert.Null(outcome.StatusCode);
        Assert.Empty(wire.Sent);
    }

    [Theory]
    [InlineData("http://localhost:5000/hook")]
    [InlineData("http://api.localhost:5000/hook")]
    [InlineData("http://127.0.0.1:5000/hook")]
    [InlineData("http://[::1]:5000/hook")]
    [InlineData("http://192.168.1.20/hook")]
    [InlineData("http://10.0.0.5:8080/hook")]
    [InlineData("http://tool.local/hook")]
    public async Task ALocalRunStillDeliversToLoopbackAndPrivateAddresses(string url)
    {
        var (client, wire) = Client(allowPublicTargets: false);

        var outcome = await client.Post(new Uri(url), "{}", "d-1", null, null, CancellationToken.None);

        Assert.True(outcome.Succeeded);
        Assert.Equal(new Uri(url), Assert.Single(wire.Sent));
    }

    /// <summary>The default: a server delivers to the public internet, which is where makers live.</summary>
    [Fact]
    public async Task AServerDeliversToPublicTargets()
    {
        var (client, wire) = Client(allowPublicTargets: true);

        var outcome = await client.Post(PublicHook, "{}", "d-1", null, null, CancellationToken.None);

        Assert.True(outcome.Succeeded);
        Assert.Equal(PublicHook, Assert.Single(wire.Sent));
    }

    /// <summary>
    ///     Verification is a POST to whatever the maker typed, so it obeys the same gate — a local
    ///     run must not probe a public host on a maker's behalf any more than deliver to it.
    /// </summary>
    [Fact]
    public async Task VerificationObeysTheSameGate()
    {
        var (client, wire) = Client(allowPublicTargets: false);

        var outcome = await client.Verify(PublicHook, "hash", null, null, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Equal(WebhookFailureReason.RefusedTarget, outcome.Reason);
        Assert.Empty(wire.Sent);
    }

    /// <summary>
    ///     A refusal is final. Backed off like a timeout, every delivery a production copy arrived
    ///     with would cycle through the retry sweep five times over an hour, on every local boot;
    ///     abandoned on the spot, the row records why and the sweep never picks it up again.
    /// </summary>
    [Fact]
    public async Task ARefusedDeliveryIsAbandonedOnTheSpotNotRetried()
    {
        var now = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
        var toolId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var tool = Tool.Create(toolId, Guid.NewGuid(), Name.From("Planner"), now);
        tool.SetWebhook(WebhookMode.ScorePush, PublicHook, 0, hasOutboundHeader: false);
        tool.MarkWebhookVerified(now);

        var deliveries = new Mock<IWebhookDeliveryRepository>();
        deliveries.Setup(d => d.Get(deliveryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookDeliveryRecord(deliveryId, toolId, Guid.NewGuid(), MixEnum.Phoenix2,
                WebhookMode.ScorePush, "d-abc", "{}", now, 0, DeliveryStatus.Pending, null,
                WebhookFailureReason.None, null, null, null, false));
        var tools = new Mock<IToolRepository>();
        tools.Setup(t => t.GetTool(toolId, It.IsAny<CancellationToken>())).ReturnsAsync(tool);
        var secrets = new Mock<IToolSecretReader>();
        secrets.Setup(s => s.GetOutboundHeader(toolId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, null));
        var client = new Mock<IWebhookDeliveryClient>();
        client.Setup(c => c.Post(PublicHook, "{}", "d-abc", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebhookDeliveryOutcome.Refused());
        var dispatcher = new WebhookDeliveryDispatcher(deliveries.Object, client.Object, tools.Object,
            secrets.Object, FakeDateTime.At(now).Object);

        var delivered = await dispatcher.Attempt(deliveryId, CancellationToken.None);

        Assert.False(delivered);
        deliveries.Verify(d => d.RecordFailure(deliveryId, 1, WebhookFailureReason.RefusedTarget, null, null, 0,
            null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
