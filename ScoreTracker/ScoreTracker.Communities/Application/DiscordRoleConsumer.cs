using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.Communities.Contracts.Messages;
using ScoreTracker.Identity.Contracts.Events;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.SharedKernel.Enums;

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
internal sealed class DiscordRoleConsumer : IConsumer<ReconcileDiscordRolesCommand>,
    IConsumer<ExternalLoginAddedEvent>,
    IConsumer<ExternalLoginRemovedEvent>,
    IConsumer<SweepDiscordRolesCommand>,
    IConsumer<ReconcileGuildMemberCommand>,
    IConsumer<PlayerTitlesChangedEvent>
{
    private readonly ILogger<DiscordRoleConsumer> _logger;
    private readonly IDiscordRoleService _saga;

    public DiscordRoleConsumer(IDiscordRoleService saga, ILogger<DiscordRoleConsumer> logger)
    {
        _saga = saga;
        _logger = logger;
    }

    /// <summary>
    ///     Holding a title is account-wide, so this fans out to every community the player is in
    ///     that hands out roles. Phoenix 2 only — a Phoenix 1 title moves nothing here.
    /// </summary>
    public async Task Consume(ConsumeContext<PlayerTitlesChangedEvent> context)
    {
        if (context.Message.Mix != MixEnum.Phoenix2) return;
        try
        {
            await _saga.ReconcileUserEverywhere(context.Message.UserId, context.CancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not settle Discord roles after a title change for {UserId}",
                context.Message.UserId);
        }
    }

    /// <summary>
    ///     Somebody linked a sign-in. Until now this was the one step of ordinary onboarding that
    ///     fired nothing: the community join happens before there is a Discord account, and the
    ///     server join happens before there is a site account to find.
    /// </summary>
    public Task Consume(ConsumeContext<ExternalLoginAddedEvent> context) =>
        Settle(context.Message.UserId, context.Message.LoginProviderName, "linked",
            context.CancellationToken);

    /// <summary>
    ///     Somebody unlinked a sign-in. Only Discord matters here: a role granted off the back of
    ///     a linked account must not outlive the link, and this is the one change nothing else
    ///     reports — without it the roles stand until the nightly sweep.
    /// </summary>
    public Task Consume(ConsumeContext<ExternalLoginRemovedEvent> context) =>
        Settle(context.Message.UserId, context.Message.LoginProviderName, "unlinked",
            context.CancellationToken);

    private async Task Settle(Guid userId, string provider, string what, CancellationToken cancellationToken)
    {
        if (!provider.Equals(DiscordRoleSaga.DiscordProvider, StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            await _saga.ReconcileUserEverywhere(userId, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not settle Discord roles after {UserId} {What} Discord", userId, what);
        }
    }

    /// <summary>The nightly backstop. See <see cref="SweepDiscordRolesCommand" />.</summary>
    public async Task Consume(ConsumeContext<SweepDiscordRolesCommand> context)
    {
        try
        {
            await _saga.SweepAll(context.CancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "The Discord role sweep did not finish");
        }
    }

    /// <summary>
    ///     A new arrival. Silent for the overwhelming majority of them: somebody with no PIU
    ///     Scores account, or a server no community has designated, stops after one read.
    /// </summary>
    public async Task Consume(ConsumeContext<ReconcileGuildMemberCommand> context)
    {
        var message = context.Message;
        try
        {
            await _saga.ReconcileGuildMember(message.GuildId, message.DiscordUserId,
                context.CancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not settle Discord roles for {DiscordUserId} joining {GuildId}",
                message.DiscordUserId, message.GuildId);
        }
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
