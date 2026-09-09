namespace ScoreTracker.Communities.Contracts.Messages;

/// <summary>
///     Somebody walked into a Discord server. Published by the bot host, which sees the gateway
///     event but cannot reach the reconcile — that lives inside this vertical.
///     <para>
///         Joining a server is the ONE of the four facts a role hangs on that changes outside the
///         system without telling us, which is the whole reason the Server Members intent is
///         enabled. Two snowflakes and nothing else, so the message cannot lose anything in
///         transit.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ReconcileGuildMemberCommand(ulong GuildId, ulong DiscordUserId);
