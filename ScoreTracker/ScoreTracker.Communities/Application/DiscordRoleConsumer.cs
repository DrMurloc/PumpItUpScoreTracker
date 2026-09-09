using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.Communities.Contracts.Messages;

namespace ScoreTracker.Communities.Application;

/// <summary>
///     Runs the reconcile off the bus, so a join, a ban or an admin edit returns to the player
///     without waiting on Discord's REST API.
///     <para>
///         Failures are logged and swallowed. A role that did not land is not worth failing the
///         action that triggered it, and the hourly sweep re-settles anyone this missed — the same
///         property that lets the whole feature survive an in-memory transport.
///     </para>
/// </summary>
internal sealed class DiscordRoleConsumer : IConsumer<ReconcileDiscordRolesCommand>
{
    private readonly ILogger<DiscordRoleConsumer> _logger;
    private readonly IDiscordRoleService _saga;

    public DiscordRoleConsumer(IDiscordRoleService saga, ILogger<DiscordRoleConsumer> logger)
    {
        _saga = saga;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReconcileDiscordRolesCommand> context)
    {
        var message = context.Message;
        try
        {
            if (message.UserId is { } userId)
                await _saga.ReconcileOne(message.CommunityId, userId, context.CancellationToken);
            else
                await _saga.ReconcileCommunity(message.CommunityId, context.CancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not settle Discord roles for community {CommunityId} member {UserId}",
                message.CommunityId, message.UserId);
        }
    }
}
