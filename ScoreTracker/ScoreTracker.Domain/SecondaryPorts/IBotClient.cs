using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IBotClient : IDisposable
    {
        public Task Start(CancellationToken cancellationToken = default);
        public Task Stop(CancellationToken cancellationToken = default);

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
    }
}
