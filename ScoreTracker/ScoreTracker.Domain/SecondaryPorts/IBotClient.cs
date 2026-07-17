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
        ///     and follows up with the returned card or text.
        /// </summary>
        public Task RegisterCommands(
            IReadOnlyList<BotCommandDefinition> commands,
            Func<BotInteraction, Task<BotReply>> onInteraction,
            Func<BotAutocompleteRequest, Task<IReadOnlyList<BotOptionChoice>>> onAutocomplete);

        // Legacy string-based slash registration — the only remaining caller is the
        // pre-/piu bot host, which moves onto RegisterCommands next. Removed once nothing
        // calls it.
        public Task RegisterSlashCommand(string name, string description, string response,
            Func<ulong, ulong, IDictionary<string, string>, Task> execution,
            IEnumerable<(string name, string description, bool isRequired)> options, bool requireChannelAdmin = false);

        public void WhenReady(Func<Task> execution);
    }
}
