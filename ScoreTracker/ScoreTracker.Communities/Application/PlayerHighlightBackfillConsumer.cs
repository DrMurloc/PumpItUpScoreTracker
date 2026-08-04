using System.Text.Json;
using System.Text.Json.Serialization;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Communities.Contracts.Messages;
using ScoreTracker.Communities.Domain;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Commands;

namespace ScoreTracker.Communities.Application;

/// <summary>
///     Copies pre-move win payloads into PlayerProgress's ledger (docs/design/rivals.md §4.3), so
///     the cutover doesn't blank every highlights feed for 30 days while new rows accumulate.
///     <para>
///         Reads from this vertical because this vertical owns the source rows. Writes through a
///         published command because it does not own the destination — and PlayerProgress cannot
///         reach back here without closing a reference cycle.
///     </para>
///     One-shot and idempotent: the destination collides on the event id, so a second run costs a
///     scan and changes nothing.
/// </summary>
internal sealed class PlayerHighlightBackfillConsumer : IConsumer<BackfillPlayerHighlightsCommand>
{
    // Must match the writer's options exactly — enums rode out as strings, so they must ride
    // back in as strings or every WinKind lands on whatever member happens to be zero.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ICommunityHighlightRepository _index;
    private readonly ILogger<PlayerHighlightBackfillConsumer> _logger;
    private readonly IMediator _mediator;

    public PlayerHighlightBackfillConsumer(ICommunityHighlightRepository index, IMediator mediator,
        ILogger<PlayerHighlightBackfillConsumer> logger)
    {
        _index = index;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<BackfillPlayerHighlightsCommand> context)
    {
        var legacy = await _index.GetLegacyPayloads(context.CancellationToken);
        var copied = 0;
        var skipped = 0;

        foreach (var row in legacy)
        {
            // A stale-schema payload is a summary of a moment described before the vocabulary
            // was complete. The reader would refuse to render it anyway, so it is not worth
            // copying forward.
            if (row.SchemaVersion != PlayerHighlightSchema.CurrentVersion)
            {
                skipped++;
                continue;
            }

            List<SignificantWin>? wins;
            try
            {
                wins = JsonSerializer.Deserialize<List<SignificantWin>>(row.Payload, SerializerOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Skipping unreadable highlight payload for event {EventId}", row.EventId);
                skipped++;
                continue;
            }

            if (wins is not { Count: > 0 })
            {
                skipped++;
                continue;
            }

            var stored = await _mediator.Send(new StorePlayerHighlightCommand(row.EventId, row.UserId, row.Mix,
                row.OccurredAt, row.SessionId, wins), context.CancellationToken);
            if (stored) copied++;
        }

        _logger.LogInformation("Backfilled {Copied} highlight payloads ({Skipped} skipped of {Total})",
            copied, skipped, legacy.Count);
    }
}
