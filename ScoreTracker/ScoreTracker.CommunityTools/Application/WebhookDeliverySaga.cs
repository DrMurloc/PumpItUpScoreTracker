using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.CommunityTools.Application;

/// <summary>
///     Turns one player's score batch into one delivery per subscribed tool, and drives the attempt.
///     <para>
///         Consumes the Domain event rather than being called by the importer: the fan-out has no
///         business on the import's critical path, and the events were designed as webhook bodies
///         under ADR-001 D3 for exactly this.
///     </para>
/// </summary>
internal sealed class WebhookDeliverySaga : IConsumer<PlayerScoresUpdatedEvent>
{
    /// <summary>
    ///     Chunk size. Measured against real usage the median import is 10 changes and 93.6% fit in
    ///     one delivery, so the tail link is a rare path rather than the common one — and a
    ///     first-time import of 3,000 scores costs us one 15 KB POST, not thirty.
    /// </summary>
    public const int MaxChangesPerDelivery = 100;

    private readonly IWebhookDeliveryDispatcher _dispatcher;
    private readonly ILogger<WebhookDeliverySaga> _logger;
    private readonly IToolRepository _tools;
    private readonly IUserReader _users;

    public WebhookDeliverySaga(IToolRepository tools, IWebhookDeliveryDispatcher dispatcher,
        IUserReader users, ILogger<WebhookDeliverySaga> logger)
    {
        _tools = tools;
        _dispatcher = dispatcher;
        _users = users;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PlayerScoresUpdatedEvent> context)
    {
        var message = context.Message;
        var toolIds = await _tools.GetToolIdsReading(message.UserId, context.CancellationToken);
        if (toolIds.Count == 0) return;

        var player = await BuildPlayerBlock(message.UserId, message.Mix, context.CancellationToken);
        if (player is null) return;

        foreach (var toolId in toolIds)
        {
            var tool = await _tools.GetTool(toolId, context.CancellationToken);
            if (tool is null) continue;

            // Session mode is delivered inline during the import, where the sid exists — never from
            // here, which runs after the fact and has no credential to forward.
            if (tool.WebhookMode is WebhookMode.None or WebhookMode.PiuGameSession) continue;
            // An unverified URL is a claim, not a destination. Skipping here rather than failing the
            // delivery keeps the console clean: nothing was attempted, so nothing failed.
            if (!tool.CanDeliver) continue;
            if (tool.Mixes.Count > 0 && !tool.Mixes.Contains(message.Mix)) continue;

            var changes = tool.WebhookMode == WebhookMode.ScorePush
                ? message.Changes.Take(MaxChangesPerDelivery).Select(c => Map(c, message.Mix)).ToArray()
                : Array.Empty<DeliveryPayload.Change>();

            try
            {
                await _dispatcher.Dispatch(tool, player, message.SessionId, changes,
                    hasMore: tool.WebhookMode == WebhookMode.ScorePush
                             && message.Changes.Count > MaxChangesPerDelivery,
                    isTest: false, context.CancellationToken);
            }
            catch (Exception e)
            {
                // One tool's endpoint must not cost the others their delivery.
                _logger.LogWarning(e, "Webhook dispatch failed for tool {ToolId}", toolId);
            }
        }
    }

    /// <summary>
    ///     Read through the published IUserReader port rather than by asking Identity or
    ///     OfficialMirror — a vertical does not reference another vertical.
    ///     <para>
    ///         User.GameTag is the right source anyway: it is the site's single tag, rewritten by
    ///         every import, which is exactly the one-tag model the payload promises. The per-mix
    ///         rows OfficialMirror keeps are scrape snapshots, not separate identities.
    ///     </para>
    /// </summary>
    private async Task<DeliveryPayload.PlayerBlock?> BuildPlayerBlock(Guid userId, MixEnum mix,
        CancellationToken cancellationToken)
    {
        var user = await _users.GetUser(userId, cancellationToken);
        if (user is null) return null;

        return new DeliveryPayload.PlayerBlock(mix.ToString(), DeliveryPayload.ScoringModelOf(mix),
            userId, user.Name.ToString(), user.GameTag?.ToString());
    }

    /// <summary>
    ///     Projects a ledger change onto the wire shape. On a legacy mix the numbers are era-scale
    ///     and the letter grade is the meaningful field, so the score slots stay null rather than
    ///     handing a consumer a number that looks like a Phoenix score.
    /// </summary>
    private static DeliveryPayload.Change Map(PlayerScoresUpdatedEvent.ScoreChange change, MixEnum mix)
    {
        if (!mix.UsesLegacyScoring())
            return new DeliveryPayload.Change(change.ChartId, change.IsNewPass, change.OldScore,
                change.NewScore, null, null, change.Plate, change.IsBroken);

        return new DeliveryPayload.Change(change.ChartId, change.IsNewPass, null, null,
            LetterGrade(change.OldScore, mix), LetterGrade(change.NewScore, mix), change.Plate,
            change.IsBroken);
    }

    private static string? LetterGrade(int? score, MixEnum mix)
    {
        return score is null ? null : PhoenixScore.From(score.Value).LetterGradeFor(mix).GetName();
    }
}
