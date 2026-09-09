using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Events;

namespace ScoreTracker.Communities.Application;

/// <summary>
///     Deletes a purged account's community memberships. Idempotent — the purge event re-fires
///     daily for a week.
///     <para>
///         The Discord roles come off FIRST, and in this consumer rather than a second one racing
///         it. The grant row is the last handle on which Discord account we granted against —
///         Identity drops the external login on the very first purge pass — and deleting it before
///         the roles are taken back would leave them standing in the server forever, with nobody
///         left to remove them.
///     </para>
/// </summary>
internal sealed class AccountPurgeConsumer : IConsumer<AccountPurgeStartedEvent>
{
    private readonly IDiscordRoleService _discordRoles;
    private readonly ILogger<AccountPurgeConsumer> _logger;
    private readonly IAccountPurgeRepository _purge;

    public AccountPurgeConsumer(IAccountPurgeRepository purge, IDiscordRoleService discordRoles,
        ILogger<AccountPurgeConsumer> logger)
    {
        _purge = purge;
        _discordRoles = discordRoles;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AccountPurgeStartedEvent> context)
    {
        var userId = context.Message.RetiredUserId;
        try
        {
            await _discordRoles.RemoveAllForUser(userId, context.CancellationToken);
        }
        catch (Exception e)
        {
            // Discord being unreachable must not stop an account's data being deleted. The event
            // re-fires daily for a week, so the next pass tries again while the row still exists.
            _logger.LogError(e, "Could not take back Discord roles for purged account {UserId}", userId);
        }

        await _purge.DeleteAllForUser(userId, context.CancellationToken);
    }
}
