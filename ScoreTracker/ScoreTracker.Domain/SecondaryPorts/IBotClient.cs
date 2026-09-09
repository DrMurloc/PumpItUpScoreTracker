using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IBotClient : IDisposable
    {
        public Task Start(CancellationToken cancellationToken = default);
        public Task Stop(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Where the gateway socket stands right now. Slash commands arrive over the gateway,
        ///     so a socket that is not Connected means no command can reach the bot even while
        ///     REST sends keep working (docs/design/discord-overhaul.md §10).
        /// </summary>
        public BotGatewayStatus Status { get; }

        /// <summary>
        ///     Discards the socket client and starts a fresh one, keeping the registered commands.
        ///     A fresh client identifies on the generic gateway and is handed a new resume host,
        ///     which is the way out of the reconnect loop Discord.Net enters when its pinned
        ///     resume host stops answering. Throws if the client was never started. Safe to call
        ///     while sends are in flight: the replaced client keeps its REST side for a grace
        ///     period after the swap, so a fan-out that started on it finishes there.
        /// </summary>
        public Task Restart(CancellationToken cancellationToken = default);

        public Task SendMessage(string message, ulong channelId, CancellationToken cancellationToken = default)
        {
            return SendMessages(new[] { message }, new[] { channelId }, cancellationToken);
        }

        public Task SendMessages(IEnumerable<string> messages, IEnumerable<ulong> channelIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Structured cards (Discord Components V2). A channel whose rich send fails
        ///     receives the message's plain-text fallback instead — an announcement never
        ///     silently drops on a rendering problem.
        /// </summary>
        public Task SendRichMessages(IEnumerable<RichBotMessage> messages, IEnumerable<ulong> channelIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Whether the bot can currently post in the channel (present, visible, and
        ///     send-permitted). The registration flow probes with this so a channel the bot
        ///     can't reach fails registration up front instead of going silently dark.
        /// </summary>
        public Task<bool> CanPostToChannel(ulong channelId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Registers the bot's command tree atomically (replacing any previously
        ///     registered commands) and wires the invocation and autocomplete handlers.
        ///     Reply visibility follows each subcommand's <see cref="BotSubCommand.Ephemeral" />
        ///     flag; the adapter defers accordingly, invokes <paramref name="onInteraction" />,
        ///     and follows up with the returned card or text. May be called before the socket is
        ///     up: the tree is published once it is, and the handlers follow every client
        ///     instance the adapter builds, so they survive a gateway restart.
        /// </summary>
        public Task RegisterCommands(
            IReadOnlyList<BotCommandDefinition> commands,
            Func<BotInteraction, Task<BotReply>> onInteraction,
            Func<BotAutocompleteRequest, Task<IReadOnlyList<BotOptionChoice>>> onAutocomplete);

        /// <summary>
        ///     Runs <paramref name="execution" /> on the current client instance's Ready. Binds to
        ///     that instance only, so it does not survive a restart. Kept for the exploration
        ///     canaries; the app registers commands through <see cref="RegisterCommands" />.
        /// </summary>
        public void WhenReady(Func<Task> execution);

        /// <summary>
        ///     The server, or null when the bot is not in it (or was removed). The role page
        ///     reads this to say whether the designation still points anywhere.
        /// </summary>
        public Task<BotGuild?> GetGuild(ulong guildId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Every role in the server, each already carrying whether the bot may assign it —
        ///     see <see cref="BotGuildRole.BlockedReason" />. Empty when the bot is not in the
        ///     server. Ordered strongest-first, the way Discord's own settings list reads.
        /// </summary>
        public Task<IReadOnlyList<BotGuildRole>> GetGuildRoles(ulong guildId,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     The role ids a member currently holds, or <b>null when they are not in the
        ///     server</b> — one call answers both "are they in it" and "what do they have", which
        ///     is exactly the pair a reconcile needs. Does not require the members intent:
        ///     fetching one member by id is not gated, only listing them all is.
        /// </summary>
        public Task<IReadOnlyCollection<ulong>?> GetMemberRoles(ulong guildId, ulong userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Every member of the server and the roles they hold, in one call. Somebody absent
        ///     from the result is not in the server — the same distinction
        ///     <see cref="GetMemberRoles" /> draws with null.
        ///     <para>
        ///         For settling a whole community at once, where one fetch per member is the
        ///         difference between a pass costing a second and costing a minute. Unlike the
        ///         single-member read this DOES need the Server Members intent, which the
        ///         application has; without it Discord returns nothing and callers see an empty
        ///         server rather than an error.
        ///     </para>
        /// </summary>
        public Task<IReadOnlyDictionary<ulong, IReadOnlyCollection<ulong>>> GetGuildMemberRoles(
            ulong guildId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Grants a role. A no-op if they already hold it. Throws if the bot cannot assign it —
        ///     callers check <see cref="BotGuildRole.CanAssign" /> first rather than discovering
        ///     it here.
        /// </summary>
        public Task AddRole(ulong guildId, ulong userId, ulong roleId,
            CancellationToken cancellationToken = default);

        /// <summary>Removes a role. A no-op if they do not hold it.</summary>
        public Task RemoveRole(ulong guildId, ulong userId, ulong roleId,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Runs <paramref name="onMemberJoined" /> whenever someone joins a server the bot is
        ///     in. Requires the Server Members privileged intent, which is enabled on the
        ///     application; without it Discord simply never raises the event and this stays
        ///     silent. Like <see cref="RegisterCommands" />, the handler follows every client
        ///     instance the adapter builds, so it survives a gateway restart.
        /// </summary>
        public void OnMemberJoined(Func<ulong, ulong, Task> onMemberJoined);
    }
}
