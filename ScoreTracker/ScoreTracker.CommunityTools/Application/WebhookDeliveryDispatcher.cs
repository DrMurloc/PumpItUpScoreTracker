using System.Text.Json;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.CommunityTools.Application;

/// <summary>
///     Builds, records and attempts one delivery. Split from the fan-out so the retry sweep and the
///     test-delivery button drive the same code — three callers, one definition of what a delivery
///     is.
/// </summary>
internal interface IWebhookDeliveryDispatcher
{
    Task Dispatch(Tool tool, DeliveryPayload.PlayerBlock player, Guid? sessionId,
        IReadOnlyList<DeliveryPayload.Change> changes, bool hasMore, bool isTest,
        CancellationToken cancellationToken);

    /// <summary>Re-attempts a delivery whose body we still hold. Null when the body has aged out.</summary>
    Task<bool> Attempt(Guid deliveryId, CancellationToken cancellationToken);
}

internal sealed class WebhookDeliveryDispatcher : IWebhookDeliveryDispatcher
{
    /// <summary>MVC's own defaults, so the body a tool receives matches the API's conventions.</summary>
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    private readonly IWebhookDeliveryClient _client;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IWebhookDeliveryRepository _deliveries;
    private readonly IToolRepository _tools;
    private readonly IToolSecretReader _secrets;

    public WebhookDeliveryDispatcher(IWebhookDeliveryRepository deliveries, IWebhookDeliveryClient client,
        IToolRepository tools, IToolSecretReader secrets, IDateTimeOffsetAccessor dateTime)
    {
        _deliveries = deliveries;
        _client = client;
        _tools = tools;
        _secrets = secrets;
        _dateTime = dateTime;
    }

    public async Task Dispatch(Tool tool, DeliveryPayload.PlayerBlock player, Guid? sessionId,
        IReadOnlyList<DeliveryPayload.Change> changes, bool hasMore, bool isTest,
        CancellationToken cancellationToken)
    {
        // The saga filters already, and this is the last gate before a POST leaves the process —
        // three callers reach it and only one of them is the saga.
        if (!tool.CanDeliver) return;

        var now = _dateTime.Now;
        // A test delivery is marked in three places — the flag, the id prefix, and the log — so a
        // maker's production database can never mistake one for real data.
        var deliveryId = (isTest ? "test-" : "d-") + Guid.NewGuid().ToString("N")[..12];

        var payload = new DeliveryPayload(deliveryId, DeliveryPayload.CurrentSchemaVersion, now, isTest,
            player, sessionId, changes,
            hasMore ? $"/api/v2/players/{player.UserId}/scores?mix={player.Mix}" : null);

        var body = JsonSerializer.Serialize(payload, Wire);

        // Recorded before it is attempted: the bus is in-memory, so a process death mid-flight
        // would otherwise lose the delivery with no trace and no retry.
        var id = await _deliveries.Enqueue(tool.Id, player.UserId,
            Enum.Parse<ScoreTracker.SharedKernel.Enums.MixEnum>(player.Mix), tool.WebhookMode,
            deliveryId, body, now, isTest, cancellationToken);

        await Attempt(id, cancellationToken);
    }

    public async Task<bool> Attempt(Guid deliveryId, CancellationToken cancellationToken)
    {
        var delivery = await _deliveries.Get(deliveryId, cancellationToken);
        if (delivery?.Body is null) return false;

        var tool = await _tools.GetTool(delivery.ToolId, cancellationToken);
        // Re-checked on every retry: a maker who changes their URL mid-backoff has un-verified it,
        // and the queued body must not follow them to somewhere unproven.
        if (tool is null || !tool.CanDeliver) return false;

        var (headerName, headerValue) = await _secrets.GetOutboundHeader(tool.Id, cancellationToken);
        var outcome = await _client.Post(tool.WebhookUrl, delivery.Body, delivery.DeliveryId,
            headerName, headerValue, cancellationToken);

        if (outcome.Succeeded)
        {
            // Keep the body only when it is the tool's newest — the console's sample of what we sent.
            var latest = await _deliveries.GetLatestWithBody(tool.Id, cancellationToken);
            var keepBody = latest is null || latest.Id == delivery.Id;
            await _deliveries.RecordSuccess(delivery.Id, delivery.Attempt + 1,
                outcome.StatusCode ?? 200, outcome.LatencyMs, keepBody, cancellationToken);
            return true;
        }

        var attempt = delivery.Attempt + 1;
        await _deliveries.RecordFailure(delivery.Id, attempt, outcome.Reason, outcome.StatusCode,
            outcome.RemoteBodySnippet, outcome.LatencyMs,
            WebhookRetry.NextAttemptAfter(attempt, _dateTime.Now), cancellationToken);
        return false;
    }
}

/// <summary>
///     Reads a tool's outbound header. Separate from <see cref="IToolRepository" /> because it is
///     the one value in the vertical that must never travel with a tool record — a query that
///     returns a Tool must not double as a way to read the secret a maker authenticates us by.
/// </summary>
internal interface IToolSecretReader
{
    Task<(string? Name, string? Value)> GetOutboundHeader(Guid toolId,
        CancellationToken cancellationToken = default);

    Task SetOutboundHeader(Guid toolId, string? name, string? value,
        CancellationToken cancellationToken = default);
}
