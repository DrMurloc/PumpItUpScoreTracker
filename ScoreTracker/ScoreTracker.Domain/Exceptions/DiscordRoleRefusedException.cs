using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.Exceptions;

/// <summary>
///     Discord turned down a role write. The bot adapter throws this in place of the library's own
///     exception, so the reconcile can tell a refusal the whole server shares from one about a single
///     member without referencing Discord.Net or reading its message.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class DiscordRoleRefusedException : Exception
{
    public DiscordRoleRefusedException(BotRoleRefusal reason, int? discordCode, Exception? inner = null)
        : base($"Discord refused the role change ({reason}{(discordCode is { } code ? $", {code}" : "")}).",
            inner)
    {
        Reason = reason;
        DiscordCode = discordCode;
    }

    public BotRoleRefusal Reason { get; }

    /// <summary>Discord's own error number, when Discord sent one.</summary>
    public int? DiscordCode { get; }

    /// <summary>
    ///     True when the answer would be the same for every member — nothing about who the role was
    ///     for changes a missing permission or the server's 2FA rule — so a pass stops at the first
    ///     one instead of asking again per member (docs/design/discord-role-management.md D28).
    /// </summary>
    public bool ServerWide => Reason is BotRoleRefusal.NoAccess or BotRoleRefusal.NoPermission
        or BotRoleRefusal.TwoFactorRequired;
}
