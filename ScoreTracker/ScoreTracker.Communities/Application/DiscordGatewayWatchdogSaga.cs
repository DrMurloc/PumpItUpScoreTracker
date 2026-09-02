using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.Communities.Contracts.Messages;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Communities.Application;

/// <summary>
///     Replaces the bot's socket client when its gateway has been down too long. Discord.Net
///     pins the resume host it was handed at READY and retries it forever when that host
///     answers 503; only a fresh client, identifying on the generic gateway, is handed a new
///     one. Slash commands arrive over the gateway, so the loop means commands silently die
///     while REST sends keep working — fourteen hours of it on 2026-09-01
///     (docs/design/discord-overhaul.md §10).
///     <para>
///         Never throws: a failed restart is logged and the next tick retries, so MassTransit
///         never faults the trigger. A bot that was never started (no token: local dev, E2E)
///         is left alone.
///     </para>
/// </summary>
internal sealed class DiscordGatewayWatchdogSaga : IConsumer<CheckDiscordGatewayCommand>
{
    /// <summary>
    ///     Every healthy resume in the incident's logs took about a second and the worst benign
    ///     flap recovered in thirty; five minutes is comfortably past both. Picked, not tuned.
    /// </summary>
    public static readonly TimeSpan RestartAfter = TimeSpan.FromMinutes(5);

    private readonly IBotClient _bot;
    private readonly ILogger<DiscordGatewayWatchdogSaga> _logger;

    public DiscordGatewayWatchdogSaga(IBotClient bot, ILogger<DiscordGatewayWatchdogSaga> logger)
    {
        _bot = bot;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CheckDiscordGatewayCommand> context)
    {
        var status = _bot.Status;
        if (status.State != BotGatewayState.Disconnected || status.DisconnectedFor < RestartAfter) return;

        _logger.LogWarning("Discord gateway has been down for {Duration}; restarting the bot client",
            status.DisconnectedFor);
        try
        {
            await _bot.Restart(context.CancellationToken);
            _logger.LogInformation("Discord bot client restarted after {Duration} without a gateway",
                status.DisconnectedFor);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Discord bot client restart failed; the next check retries");
        }
    }
}
