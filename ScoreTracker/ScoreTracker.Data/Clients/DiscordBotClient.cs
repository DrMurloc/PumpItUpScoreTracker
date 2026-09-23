using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Configuration;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Clients;

public sealed class DiscordBotClient : IBotClient
{
    /// <summary>
    ///     How long a replaced client keeps its REST side after the swap. A feed fan-out to a few
    ///     dozen channels takes tens of seconds under Discord's rate limits; two minutes lets one
    ///     that straddled a restart finish on the client it started with. Picked, not tuned.
    /// </summary>
    private static readonly TimeSpan ReplacedClientGrace = TimeSpan.FromMinutes(2);

    private readonly DiscordConfiguration _configuration;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private volatile bool _commandsPublished;
    private Func<ulong, ulong, Task>? _memberJoined;
    private CommandRegistration? _registration;
    private GatewaySession? _session;

    public DiscordBotClient(ILogger<DiscordBotClient> logger, IOptions<DiscordConfiguration> options)
    {
        _logger = logger;
        _configuration = options.Value;
    }

    /// <summary>
    ///     The live socket client. Callers snapshot it once, so a restart in the middle of a
    ///     send cannot pull the client out from under it.
    /// </summary>
    private DiscordSocketClient Client =>
        _session?.Client ?? throw new InvalidOperationException("Client was never started");

    public BotGatewayStatus Status => _session?.State.Status ?? BotGatewayStatus.NotStarted;

    public async Task Start(CancellationToken cancellationToken = default)
    {
        if (_session != null) throw new Exception("Discord client was already started");

        _session = await OpenSession();
    }

    /// <summary>
    ///     Replaces the socket client with a fresh one, keeping the registered commands. The
    ///     replacement is built, logged in and started before the swap, so the client is never
    ///     null. The old one has its gateway stopped at once and its handlers unhooked, but keeps
    ///     its REST side for <see cref="ReplacedClientGrace" />, so a send that started on it
    ///     finishes there. Serialized, so two watchdog ticks cannot replace the client twice.
    /// </summary>
    public async Task Restart(CancellationToken cancellationToken = default)
    {
        if (_session == null) throw new InvalidOperationException("Client was never started");

        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            // A shutdown that lands here must not log a fresh client in on the way out the door.
            cancellationToken.ThrowIfCancellationRequested();
            var replacement = await OpenSession();
            var replaced = Interlocked.Exchange(ref _session, replacement);
            if (replaced != null) await Retire(replaced.Client);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    /// <summary>
    ///     A retired client must never answer a command again: its handlers read the shared
    ///     registration, so if its gateway came back before disposal every command would get two
    ///     replies. Unhook first, stop the gateway, and dispose after the grace whether or not
    ///     the stop was clean.
    /// </summary>
    private async Task Retire(DiscordSocketClient client)
    {
        client.SlashCommandExecuted -= OnSlashCommand;
        client.AutocompleteExecuted -= OnAutocomplete;
        client.UserJoined -= OnUserJoined;
        try
        {
            await client.StopAsync();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "The replaced Discord client did not stop cleanly");
        }

        _ = DisposeAfterGrace(client);
    }

    private async Task DisposeAfterGrace(DiscordSocketClient client)
    {
        try
        {
            await Task.Delay(ReplacedClientGrace);
            await client.DisposeAsync();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "The replaced Discord client did not dispose cleanly");
        }
    }

    /// <summary>
    ///     Every client instance gets the same wiring, the first one and any replacement, so
    ///     the interaction handlers are subscribed exactly once per instance instead of once per
    ///     Ready. Discord.Net raises Ready on every fresh IDENTIFY; subscribing there stacked a
    ///     second handler on a mid-run re-identify and answered every command twice.
    /// </summary>
    private async Task<GatewaySession> OpenSession()
    {
        var state = new GatewayStateTracker();
        var client = new DiscordSocketClient(new DiscordSocketConfig
        {
            LogLevel = LogSeverity.Info,
            // GuildMembers is privileged and enabled on the application. It buys exactly one
            // thing: being told when somebody joins a server, which is the only fact the Discord
            // role feature needs that changes without telling us. AlwaysDownloadUsers stays OFF
            // — the member-list download is the expensive half of that intent and nothing here
            // needs it, since a single member is fetched by id over REST.
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers,
            AlwaysDownloadUsers = false
        });

        // Severity, source and exception all travel: the disconnect reason Discord.Net reports
        // as an exception-only entry is the whole diagnosis of a reconnect loop.
        client.Log += msg =>
        {
            _logger.Log(DiscordLogMapping.ToLogLevel(msg.Severity), msg.Exception,
                "Discord.Net {Source}: {Message}", msg.Source, DiscordLogMapping.Text(msg));
            return Task.CompletedTask;
        };
        client.SlashCommandExecuted += OnSlashCommand;
        client.AutocompleteExecuted += OnAutocomplete;
        client.UserJoined += OnUserJoined;
        // The tree is published once the socket is up. Either hook may be the one that finds
        // the registration in place; the publish itself runs once per process. Connected fires
        // on a resume as well as a fresh identify, so a failed publish retries within hours.
        client.Connected += () =>
        {
            state.Connected();
            return PublishCommands(client);
        };
        client.Disconnected += _ =>
        {
            state.Disconnected();
            return Task.CompletedTask;
        };
        client.Ready += () => PublishCommands(client);

        // Downtime counts from here: a client that never connects is as dead as one that dropped.
        state.Starting();
        try
        {
            await client.LoginAsync(TokenType.Bot, _configuration.BotToken);
            await client.StartAsync();
        }
        catch
        {
            // A half-built client owns a request queue with its own cleanup loop and nothing
            // else references it, so an undisposed one lives for the rest of the process. Login
            // fails whenever Discord's REST side is unwell, which is exactly when restarts run.
            await client.DisposeAsync();
            throw;
        }

        return new GatewaySession(client, state);
    }

    public async Task Stop(CancellationToken cancellationToken = default)
    {
        // Taking the session out first puts Status back to NotStarted, so a watchdog tick that
        // lands during shutdown finds nothing to rebuild.
        var session = Interlocked.Exchange(ref _session, null) ??
                      throw new InvalidOperationException("Client was never started");
        await session.Client.StopAsync();
        await session.Client.DisposeAsync();
    }

    /// <summary>
    ///     Binds to the current client instance's Ready only; a hook registered here does not
    ///     survive a restart. The app registers its commands through
    ///     <see cref="RegisterCommands" /> instead; this stays for the exploration canaries.
    /// </summary>
    public void WhenReady(Func<Task> execution)
    {
        Client.Ready += execution;
    }

    public void Dispose()
    {
        _session?.Client.Dispose();
        _lifecycle.Dispose();
        _publishLock.Dispose();
    }

    public async Task SendMessages(IEnumerable<string> messages, IEnumerable<ulong> channelIds,
        CancellationToken cancellationToken = default)
    {
        await SendMessages(messages, channelIds, m => m);
    }

    public async Task<bool> CanPostToChannel(ulong channelId, CancellationToken cancellationToken = default)
    {
        var client = Client;
        if (await client.GetChannelAsync(channelId) is not IGuildChannel channel) return false;
        var botUser = await channel.Guild.GetCurrentUserAsync();
        var permissions = botUser.GetPermissions(channel);
        return permissions.ViewChannel && permissions.SendMessages;
    }

    public async Task<BotGuild?> GetGuild(ulong guildId, CancellationToken cancellationToken = default)
    {
        var guild = Client.GetGuild(guildId);
        return guild == null
            ? null
            : await Task.FromResult(new BotGuild(guild.Id, guild.Name,
                guild.CurrentUser?.GuildPermissions.ManageRoles ?? false));
    }

    /// <summary>
    ///     Roles strongest-first, each carrying why the bot cannot assign it. The hierarchy test is
    ///     Discord's own: a role at or above the bot's highest position is refused, and the refusal
    ///     is invisible to the player, so it has to be decided before a write rather than after.
    /// </summary>
    public async Task<IReadOnlyList<BotGuildRole>> GetGuildRoles(ulong guildId,
        CancellationToken cancellationToken = default)
    {
        var guild = Client.GetGuild(guildId);
        if (guild == null) return Array.Empty<BotGuildRole>();

        // Hierarchy is only half the question. A bot invited WITHOUT Manage Roles passes every
        // position check and then gets 50001 Missing Access on the write — which Discord reports
        // to nobody. Asking the guild for the bot's own permission is the other half, and it is
        // the state every server invited before this feature shipped is in.
        var canManage = guild.CurrentUser?.GuildPermissions.ManageRoles ?? false;
        var ceiling = guild.CurrentUser?.Roles.Max(r => (int?)r.Position) ?? -1;
        return await Task.FromResult(guild.Roles
            .OrderByDescending(r => r.Position)
            // The missing permission wins over the position check. A bot with no Manage Roles at
            // all also sits below plenty of roles, and labelling those AboveBot tells an admin to
            // drag roles around — which cannot fix a permission that was never granted.
            .Select(r => new BotGuildRole(r.Id, r.Name, ColorOf(r),
                !canManage && !r.IsEveryone && !r.IsManaged
                    ? BotRoleBlockedReason.BotCannotManageRoles
                    : BlockedReasonFor(r, ceiling)))
            .ToArray());
    }

    private static string? ColorOf(SocketRole role) =>
        role.Color.RawValue == 0 ? null : $"#{role.Color.RawValue:X6}";

    private static BotRoleBlockedReason? BlockedReasonFor(SocketRole role, int botCeiling)
    {
        if (role.IsEveryone) return BotRoleBlockedReason.Everyone;
        if (role.IsManaged) return BotRoleBlockedReason.Managed;
        return role.Position >= botCeiling ? BotRoleBlockedReason.AboveBot : null;
    }

    /// <summary>
    ///     Null means "not in the server" — the one call that answers both halves a reconcile
    ///     needs. Deliberately a REST fetch of a single member rather than a cache read: fetching
    ///     one member by id is not gated on the members intent (only listing them all is), and the
    ///     socket's member cache is not populated because AlwaysDownloadUsers stays off.
    /// </summary>
    public async Task<IReadOnlyCollection<ulong>?> GetMemberRoles(ulong guildId, ulong userId,
        CancellationToken cancellationToken = default)
    {
        var guild = Client.GetGuild(guildId);
        if (guild == null) return null;

        var member = guild.GetUser(userId) ?? (IGuildUser?)await Client.Rest.GetGuildUserAsync(guildId, userId);
        // Not in the server. Distinct from "in it holding nothing", which is an empty set.
        return member?.RoleIds.ToArray();
    }

    /// <summary>
    ///     One walk of the server's members instead of one fetch each. Gated on the Server Members
    ///     intent, which the application has. A guild the bot cannot see reports NULL rather than
    ///     an empty roster — the two mean opposite things and the callers act on the difference.
    /// </summary>
    public async Task<IReadOnlyDictionary<ulong, IReadOnlyCollection<ulong>>?> GetGuildMemberRoles(
        ulong guildId, CancellationToken cancellationToken = default)
    {
        // Null, not empty: the socket is also null mid-reconnect, and a caller that read that as
        // "the server has nobody in it" would revoke nothing and report success.
        var guild = Client.GetGuild(guildId);
        if (guild == null) return null;

        // Populates the socket cache for THIS guild, on demand. Deliberately not what
        // AlwaysDownloadUsers does — that pays for every guild at every connect, whether or not
        // anyone is handing out roles there.
        await guild.DownloadUsersAsync();

        var roles = new Dictionary<ulong, IReadOnlyCollection<ulong>>();
        foreach (var member in guild.Users)
            roles[member.Id] = member.Roles.Select(r => r.Id).ToArray();

        return roles;
    }

    public async Task AddRole(ulong guildId, ulong userId, ulong roleId,
        CancellationToken cancellationToken = default)
    {
        var member = await Member(guildId, userId);
        if (member != null) await member.AddRoleAsync(roleId);
    }

    public async Task RemoveRole(ulong guildId, ulong userId, ulong roleId,
        CancellationToken cancellationToken = default)
    {
        var member = await Member(guildId, userId);
        if (member != null) await member.RemoveRoleAsync(roleId);
    }

    private async Task<IGuildUser?> Member(ulong guildId, ulong userId)
    {
        var guild = Client.GetGuild(guildId);
        if (guild == null) return null;
        return guild.GetUser(userId) ?? (IGuildUser?)await Client.Rest.GetGuildUserAsync(guildId, userId);
    }

    /// <summary>
    ///     Stored on the adapter rather than on a client instance, so — like the command
    ///     registration — it follows every client the adapter builds and survives a gateway
    ///     restart. Silent without the Server Members intent: Discord simply never raises the
    ///     event.
    /// </summary>
    public void OnMemberJoined(Func<ulong, ulong, Task> onMemberJoined)
    {
        _memberJoined = onMemberJoined;
    }

    public Task RegisterCommands(
        IReadOnlyList<BotCommandDefinition> commands,
        Func<BotInteraction, Task<BotReply>> onInteraction,
        Func<BotAutocompleteRequest, Task<IReadOnlyList<BotOptionChoice>>> onAutocomplete)
    {
        var client = Client;

        _registration = new CommandRegistration(commands,
            commands.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase), onInteraction, onAutocomplete);

        // Already connected (the canaries register after Ready): publish now. Otherwise the
        // Connected and Ready hooks pick the registration up when the socket comes up.
        return client.ConnectionState == ConnectionState.Connected
            ? PublishCommands(client)
            : Task.CompletedTask;
    }

    // Bulk overwrite replaces the whole global command set atomically — any commands
    // registered by an earlier build (the pre-/piu top-level commands) are dropped. Once per
    // process: the tree is static per build and Discord rate-limits command writes, so a
    // replacement client after a restart only needs its handlers, which OpenSession wired.
    private async Task PublishCommands(DiscordSocketClient client)
    {
        var registration = _registration;
        if (registration == null || _commandsPublished) return;

        await _publishLock.WaitAsync();
        try
        {
            if (_commandsPublished) return;
            await client.BulkOverwriteGlobalApplicationCommandsAsync(registration.Commands
                .Select(DiscordCommandTranslator.ToProperties).Cast<ApplicationCommandProperties>().ToArray());
            _commandsPublished = true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not publish the bot command tree; the next gateway connect retries");
        }
        finally
        {
            _publishLock.Release();
        }
    }

    /// <summary>
    ///     Someone walked into a server the bot is in. Swallows on failure: a join is a
    ///     notification, not a transaction, and the reconcile sweep covers anything missed here.
    /// </summary>
    private async Task OnUserJoined(SocketGuildUser member)
    {
        var joined = _memberJoined;
        if (joined == null || member.IsBot) return;
        try
        {
            await joined(member.Guild.Id, member.Id);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error handling a member joining guild {GuildId}", member.Guild.Id);
        }
    }

    private async Task OnSlashCommand(SocketSlashCommand command)
    {
        var registration = _registration;
        if (registration == null ||
            !registration.Definitions.TryGetValue(command.CommandName, out var definition)) return;
        var (path, options) = DiscordCommandTranslator.ResolveInvocation(command);
        var ephemeral = DiscordCommandTranslator.IsEphemeral(definition, path);
        try
        {
            await command.DeferAsync(ephemeral);
            var reply = await registration.OnInteraction(BuildInteraction(command, path, options));
            await Followup(command, reply, ephemeral);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error executing /{Command} {Path}", command.CommandName, string.Join(' ', path));
            try
            {
                await command.FollowupAsync("Something went wrong running that command.", ephemeral: ephemeral);
            }
            catch (Exception followupError)
            {
                _logger.LogWarning(followupError, "Could not send the command error follow-up");
            }
        }
    }

    private async Task OnAutocomplete(SocketAutocompleteInteraction interaction)
    {
        var registration = _registration;
        if (registration == null || !registration.Definitions.ContainsKey(interaction.Data.CommandName)) return;
        try
        {
            var (path, options) = DiscordCommandTranslator.ResolveAutocomplete(interaction);
            var focused = interaction.Data.Current;
            var request = new BotAutocompleteRequest(path, focused.Name,
                focused.Value?.ToString() ?? string.Empty, options,
                interaction.User.Id, interaction.Channel.Id, (interaction.Channel as IGuildChannel)?.GuildId);
            var choices = await registration.OnAutocomplete(request);
            await interaction.RespondAsync(choices.Take(25)
                .Select(c => new AutocompleteResult(c.Name, c.Value)));
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Autocomplete failed for /{Command}", interaction.Data.CommandName);
            try
            {
                await interaction.RespondAsync(Array.Empty<AutocompleteResult>());
            }
            catch (Exception respondError)
            {
                _logger.LogWarning(respondError, "Could not send empty autocomplete response");
            }
        }
    }

    /// <summary>
    ///     The command tree plus the app's two dispatch callbacks, kept so every client
    ///     instance can be wired from it.
    /// </summary>
    private sealed record CommandRegistration(
        IReadOnlyList<BotCommandDefinition> Commands,
        IReadOnlyDictionary<string, BotCommandDefinition> Definitions,
        Func<BotInteraction, Task<BotReply>> OnInteraction,
        Func<BotAutocompleteRequest, Task<IReadOnlyList<BotOptionChoice>>> OnAutocomplete);

    /// <summary>One socket client and the tracker watching its gateway; swapped whole on a restart.</summary>
    private sealed record GatewaySession(DiscordSocketClient Client, GatewayStateTracker State);

    private static BotInteraction BuildInteraction(SocketSlashCommand command, IReadOnlyList<string> path,
        IReadOnlyDictionary<string, string> options)
    {
        var guildUser = command.User as IGuildUser;
        var canManage = guildUser != null && command.Channel is IGuildChannel guildChannel &&
                        guildUser.GetPermissions(guildChannel).ManageChannel;
        var display = guildUser?.DisplayName ?? command.User.GlobalName ?? command.User.Username;
        return new BotInteraction(path, options, command.Channel.Id,
            (command.Channel as IGuildChannel)?.GuildId, command.User.Id, display, canManage,
            command.UserLocale, guildUser?.GuildPermissions.ManageGuild ?? false);
    }

    private async Task Followup(SocketSlashCommand command, BotReply reply, bool ephemeral)
    {
        if (reply.Card != null)
        {
            var (components, fallback) = DiscordRichMessageRenderer.Render(reply.Card, DiscordEmojiTokens.Replace);
            if (_configuration.RichScoreMessages)
                try
                {
                    await command.FollowupAsync(components: components, flags: MessageFlags.ComponentsV2,
                        ephemeral: ephemeral);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Rich command follow-up failed — falling back to plain text");
                }

            await command.FollowupAsync(TrimToLimit(DiscordEmojiTokens.Replace(fallback)), ephemeral: ephemeral);
            return;
        }

        await command.FollowupAsync(TrimToLimit(DiscordEmojiTokens.Replace(reply.Text ?? "​")), ephemeral: ephemeral);
    }

    // Discord caps a message's content at 2000 characters; a card's text budget is
    // enforced by the renderer, so this only ever clamps a long plain-text reply.
    private static string TrimToLimit(string message)
    {
        if (string.IsNullOrEmpty(message)) return "​";
        return message.Length <= 2000 ? message : message[..1999] + "…";
    }

    public async Task SendRichMessages(IEnumerable<RichBotMessage> messages, IEnumerable<ulong> channelIds,
        CancellationToken cancellationToken = default)
    {
        var client = Client;
        var rendered = messages
            .Select(m => DiscordRichMessageRenderer.Render(m, DiscordEmojiTokens.Replace))
            .ToArray();

        foreach (var channelId in channelIds)
            try
            {
                if (await client.GetChannelAsync(channelId) is not IMessageChannel channel)
                {
                    _logger.LogWarning("Channel {ChannelId} was not found", channelId);
                    continue;
                }

                await SendRichToChannel(channel, rendered, channelId);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Could not send rich messages to channel {ChannelId}", channelId);
            }
    }

    private async Task SendRichToChannel(IMessageChannel channel,
        IEnumerable<(MessageComponent Components, string FallbackText)> rendered, ulong channelId)
    {
        foreach (var (components, fallbackText) in rendered)
        {
            // The kill switch and any per-channel V2 failure both degrade to the
            // plain-text path — an announcement never silently drops.
            if (_configuration.RichScoreMessages)
                try
                {
                    await channel.SendMessageAsync(components: components,
                        flags: MessageFlags.ComponentsV2);
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Rich send to channel {ChannelId} failed — falling back to plain text", channelId);
                }

            foreach (var part in DiscordMessageSplitter.Split(DiscordEmojiTokens.Replace(fallbackText)))
                await channel.SendMessageAsync(part);
        }
    }

    private async Task SendMessages(IEnumerable<string> messageEntities, IEnumerable<ulong> channelIds,
        Func<string, string> messageRetrieval,
        Action<string, IUserMessage>? process = default)
    {
        var replacedMessages = messageEntities.Select(DiscordEmojiTokens.Replace).ToList();

        var client = Client;
        foreach (var channelId in channelIds)
            try
            {
                if (await client.GetChannelAsync(channelId) is not IMessageChannel channel)
                {
                    _logger.LogWarning("Channel {ChannelId} was not found", channelId);
                    continue;
                }

                await SendPlainToChannel(channel, replacedMessages, messageRetrieval, process, channelId);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Could not send messages to channel {ChannelId}", channelId);
            }
    }

    private async Task SendPlainToChannel(IMessageChannel channel, IEnumerable<string> messages,
        Func<string, string> messageRetrieval, Action<string, IUserMessage>? process, ulong channelId)
    {
        foreach (var message in messages)
            try
            {
                foreach (var part in DiscordMessageSplitter.Split(messageRetrieval(message)))
                {
                    var userMessage = await channel.SendMessageAsync(part);
                    if (process != null) process(message, userMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send message to channel {ChannelId}. Message: {Message}",
                    channelId, message);
            }
    }
}