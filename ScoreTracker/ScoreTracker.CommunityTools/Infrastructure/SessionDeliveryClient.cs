using System.Text.Json;
using Microsoft.Extensions.Logging;
using ScoreTracker.CommunityTools.Application;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.CommunityTools.Infrastructure;

/// <summary>
///     Forwards a piugame.com session to the tools entitled to it.
///     <para>
///         <b>Nothing here is written down.</b> No delivery row, no body, no signature sample — the
///         payload carries a live credential, and <c>RedactedString</c> is not protection at the
///         persistence boundary: it masks <c>ToString()</c> but its JSON converter round-trips the
///         real value, so serialising one into a table stores the sid in plaintext past a type that
///         looks like it is guarding you. Only the metadata reaches the console.
///     </para>
/// </summary>
internal sealed class SessionDeliveryClient : ISessionDeliveryClient
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    private readonly IToolActivityRepository _activity;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly ILogger<SessionDeliveryClient> _logger;
    private readonly IToolSecretReader _secrets;
    private readonly IWebhookDeliveryClient _client;
    private readonly IToolRepository _tools;

    public SessionDeliveryClient(IToolRepository tools, IWebhookDeliveryClient client,
        IToolSecretReader secrets, IToolActivityRepository activity,
        IDateTimeOffsetAccessor dateTime, ILogger<SessionDeliveryClient> logger)
    {
        _tools = tools;
        _client = client;
        _secrets = secrets;
        _activity = activity;
        _dateTime = dateTime;
        _logger = logger;
    }

    public async Task DeliverSession(Guid userId, MixEnum mix, RedactedString sid, string gameTag,
        CancellationToken cancellationToken = default)
    {
        // Direct grants only. GetSharesForUser returns exactly those — a tool in session mode is
        // excluded from the all-tools pool by construction, so blanket consent can never reach here.
        var shares = await _tools.GetSharesForUser(userId, cancellationToken);

        foreach (var share in shares.Where(s => s.Source == ShareSource.Direct))
        {
            var tool = await _tools.GetTool(share.ToolId, cancellationToken);
            if (tool?.WebhookUrl is null || tool.WebhookMode != WebhookMode.PiuGameSession) continue;
            if (tool.Mixes.Count > 0 && !tool.Mixes.Contains(mix)) continue;

            try
            {
                await Deliver(tool, userId, mix, sid, gameTag, cancellationToken);
            }
            catch (Exception e)
            {
                // A tool's endpoint must never fail a player's import. The sid is not logged —
                // RedactedString masks it in the message, and nothing else here carries it.
                _logger.LogWarning(e, "Session delivery failed for tool {ToolId}", tool.Id);
            }
        }
    }

    private async Task Deliver(Tool tool, Guid userId, MixEnum mix, RedactedString sid, string gameTag,
        CancellationToken cancellationToken)
    {
        var now = _dateTime.Now;
        var deliveryId = "s-" + Guid.NewGuid().ToString("N")[..12];
        var legacy = PiuTrackerSessionShape.Applies(tool.Id);

        // Built, signed and sent without ever being handed to the delivery repository.
        var body = legacy
            ? PiuTrackerSessionShape.Body(sid.Reveal())
            : JsonSerializer.Serialize(new
            {
                deliveryId,
                schemaVersion = DeliveryPayload.CurrentSchemaVersion,
                sentAt = now,
                mix = mix.ToString(),
                userId,
                gameTag,
                sid = sid.Reveal()
            }, Wire);
        var endpoint = legacy
            ? PiuTrackerSessionShape.Endpoint(tool.WebhookUrl!, gameTag)
            : tool.WebhookUrl!;

        var (headerName, headerValue) = await _secrets.GetOutboundHeader(tool.Id, cancellationToken);

        var outcome = await _client.Post(endpoint, body, deliveryId, headerName, headerValue,
            cancellationToken);

        // Metadata only. The console shows delivered or failed and nothing behind it, which is why
        // the debug page tells a session-mode maker there is no replay and no echo.
        await _activity.Record(tool.Id,
            outcome.Succeeded ? ToolActivityKind.DeliverySucceeded : ToolActivityKind.DeliveryRejected,
            now, outcome.Succeeded ? null : outcome.Reason.ToString(), cancellationToken);
    }
}
