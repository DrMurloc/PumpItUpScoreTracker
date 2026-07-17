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
    private readonly DiscordConfiguration _configuration;
    private readonly ILogger _logger;
    private DiscordSocketClient? _client;

    public DiscordBotClient(ILogger<DiscordBotClient> logger, IOptions<DiscordConfiguration> options)
    {
        _logger = logger;
        _configuration = options.Value;
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        if (_client != null) throw new Exception("Discord client was already started");

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            LogLevel = LogSeverity.Info
        });

        _client.Log += msg =>
        {
            _logger.LogInformation(msg.Message);
            return Task.CompletedTask;
        };
        await _client.LoginAsync(TokenType.Bot, _configuration.BotToken);
        await _client.StartAsync();
    }

    public async Task Stop(CancellationToken cancellationToken = default)
    {
        if (_client == null) throw new Exception("Client was never started");
        await _client.StopAsync();
        await _client.DisposeAsync();
    }

    public void WhenReady(Func<Task> execution)
    {
        if (_client == null) throw new Exception("Client was not started");
        _client.Ready += execution;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }

    public async Task SendMessages(IEnumerable<string> messages, IEnumerable<ulong> channelIds,
        CancellationToken cancellationToken = default)
    {
        await SendMessages(messages, channelIds, m => m);
    }

    public async Task<bool> CanPostToChannel(ulong channelId, CancellationToken cancellationToken = default)
    {
        if (_client == null) throw new InvalidOperationException("Client was never started");
        if (await _client.GetChannelAsync(channelId) is not IGuildChannel channel) return false;
        var botUser = await channel.Guild.GetCurrentUserAsync();
        var permissions = botUser.GetPermissions(channel);
        return permissions.ViewChannel && permissions.SendMessages;
    }

    public async Task RegisterCommands(
        IReadOnlyList<BotCommandDefinition> commands,
        Func<BotInteraction, Task<BotReply>> onInteraction,
        Func<BotAutocompleteRequest, Task<IReadOnlyList<BotOptionChoice>>> onAutocomplete)
    {
        if (_client == null) throw new InvalidOperationException("Discord client was not started");

        var definitions = commands.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        // Bulk overwrite replaces the whole global command set atomically — any commands
        // registered by an earlier build (the pre-/piu top-level commands) are dropped.
        await _client.BulkOverwriteGlobalApplicationCommandsAsync(
            commands.Select(DiscordCommandTranslator.ToProperties).Cast<ApplicationCommandProperties>().ToArray());

        _client.SlashCommandExecuted += async command =>
        {
            if (!definitions.TryGetValue(command.CommandName, out var definition)) return;
            var (path, options) = DiscordCommandTranslator.ResolveInvocation(command);
            var ephemeral = DiscordCommandTranslator.IsEphemeral(definition, path);
            try
            {
                await command.DeferAsync(ephemeral);
                var reply = await onInteraction(BuildInteraction(command, path, options));
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
        };

        _client.AutocompleteExecuted += async interaction =>
        {
            if (!definitions.ContainsKey(interaction.Data.CommandName)) return;
            try
            {
                var (path, options) = DiscordCommandTranslator.ResolveAutocomplete(interaction);
                var focused = interaction.Data.Current;
                var request = new BotAutocompleteRequest(path, focused.Name,
                    focused.Value?.ToString() ?? string.Empty, options,
                    interaction.User.Id, interaction.Channel.Id, (interaction.Channel as IGuildChannel)?.GuildId);
                var choices = await onAutocomplete(request);
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
        };
    }

    private static BotInteraction BuildInteraction(SocketSlashCommand command, IReadOnlyList<string> path,
        IReadOnlyDictionary<string, string> options)
    {
        var guildUser = command.User as IGuildUser;
        var canManage = guildUser != null && command.Channel is IGuildChannel guildChannel &&
                        guildUser.GetPermissions(guildChannel).ManageChannel;
        var display = guildUser?.DisplayName ?? command.User.GlobalName ?? command.User.Username;
        return new BotInteraction(path, options, command.Channel.Id,
            (command.Channel as IGuildChannel)?.GuildId, command.User.Id, display, canManage);
    }

    private async Task Followup(SocketSlashCommand command, BotReply reply, bool ephemeral)
    {
        if (reply.Card != null)
        {
            var (components, fallback) = DiscordRichMessageRenderer.Render(reply.Card, ReplaceEmojiTokens);
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

            await command.FollowupAsync(TrimToLimit(ReplaceEmojiTokens(fallback)), ephemeral: ephemeral);
            return;
        }

        await command.FollowupAsync(TrimToLimit(ReplaceEmojiTokens(reply.Text ?? "​")), ephemeral: ephemeral);
    }

    // Discord caps a message's content at 2000 characters; a card's text budget is
    // enforced by the renderer, so this only ever clamps a long plain-text reply.
    private static string TrimToLimit(string message)
    {
        if (string.IsNullOrEmpty(message)) return "​";
        return message.Length <= 2000 ? message : message[..1999] + "…";
    }

    private IDictionary<PhoenixLetterGrade, string> _letterGradeEmojis = new Dictionary<PhoenixLetterGrade, string>
    {
        { PhoenixLetterGrade.F, "<:piu_f:1238540776091422882>" },
        { PhoenixLetterGrade.D, "<:piu_d:1238540632591568948>" },
        { PhoenixLetterGrade.C, "<:piu_c:1238540630632824912>" },
        { PhoenixLetterGrade.B, "<:piu_b:1238540628539867240>" },
        { PhoenixLetterGrade.A, "<:piu_a:1238540428844990524>" },
        { PhoenixLetterGrade.APlus, "<:piu_aplus:1238540626983915580>" },
        { PhoenixLetterGrade.AA, "<:piu_aa:1238540431457910840>" },
        { PhoenixLetterGrade.AAPlus, "<:piu_aaplus:1238540479910641704>" },
        { PhoenixLetterGrade.AAA, "<:piu_aaa:1238540433391484928>" },
        { PhoenixLetterGrade.AAAPlus, "<:piu_aaaplus:1238540436520308746>" },
        { PhoenixLetterGrade.S, "<:piu_s:1238540781573243040>" },
        { PhoenixLetterGrade.SPlus, "<:piu_splus:1238540841719697501>" },
        { PhoenixLetterGrade.SS, "<:piu_ss:1238541129448951848>" },
        { PhoenixLetterGrade.SSPlus, "<:piu_ssplus:1238541131902615585>" },
        { PhoenixLetterGrade.SSS, "<:piu_sss:1238541133982732408>" },
        { PhoenixLetterGrade.SSSPlus, "<:piu_sssplus:1238541135681552435>" }
    };

    private readonly IDictionary<PhoenixLetterGrade, string> _brokenLetterGradeEmojis =
        new Dictionary<PhoenixLetterGrade, string>
        {
            { PhoenixLetterGrade.F, "<:piu_f_broken:1238540776993198203>" },
            { PhoenixLetterGrade.D, "<:piu_d_broken:1238540672706019420>" },
            { PhoenixLetterGrade.C, "<:piu_c_broken:1238540631534469293>" },
            { PhoenixLetterGrade.B, "<:piu_b_broken:1238540629471006855>" },
            { PhoenixLetterGrade.A, "<:piu_a_broken:1238540429956354159>" },
            { PhoenixLetterGrade.APlus, "<:piu_aplus_broken:1238540627830898758>" },
            { PhoenixLetterGrade.AA, "<:piu_aa_broken:1238540432699559936>" },
            { PhoenixLetterGrade.AAPlus, "<:piu_aaplus_broken:1238540440232394813>" },
            { PhoenixLetterGrade.AAA, "<:piu_aaa_broken:1238540434402447420>" },
            { PhoenixLetterGrade.AAAPlus, "<:piu_aaaplus_broken:1238540437665611837>" },
            { PhoenixLetterGrade.S, "<:piu_s_broken:1238540966109904906>" },
            { PhoenixLetterGrade.SPlus, "<:piu_splus_broken:1238540843334242408>" },
            { PhoenixLetterGrade.SS, "<:piu_ss_broken:1238541131130732564>" },
            { PhoenixLetterGrade.SSPlus, "<:piu_ssplus_broken:1238541132976230402>" },
            { PhoenixLetterGrade.SSS, "<:piu_sss_broken:1238541134758674583>" },
            { PhoenixLetterGrade.SSSPlus, "<:piu_sssplus_broken:1238541136545714196>" }
        };

    // Uploaded to the PIU Scores official server alongside the grade/plate bubbles
    // (owner, 2026-07-05). Color is the mix signal at inline size: Phoenix = blue,
    // Phoenix 2 = green, XX = pink.
    private readonly IDictionary<string, string> _mixEmojis = new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase)
    {
        { "Phoenix", "<:phoenix_logo:1523325598171398164>" },
        { "Phoenix2", "<:phoenix2_logo:1523325648976875704>" },
        { "XX", "<:xx_logo:1523325684259356703>" }
    };

    private readonly IDictionary<PhoenixPlate, string> _plateEmojis = new Dictionary<PhoenixPlate, string>
    {
        {
            PhoenixPlate.RoughGame, "<:piu_rg:1238540780402901033>"
        },
        { PhoenixPlate.FairGame, "<:piu_fg:1238540777890644069>" },
        { PhoenixPlate.TalentedGame, "<:piu_tg:1238541195932598353>" },
        { PhoenixPlate.MarvelousGame, "<:piu_mg:1238540779052470343>" },
        { PhoenixPlate.ExtremeGame, "<:piu_eg:1238540635343159457>" },
        { PhoenixPlate.SuperbGame, "<:piu_sg:1238540784450670713>" },
        { PhoenixPlate.UltimateGame, "<:piu_ug:1238541140429639781>" },
        { PhoenixPlate.PerfectGame, "<:piu_pg:1238540780017025185>" }
    };

    private readonly IDictionary<string, string> _difficultyShortHandEmojis =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "s1", "<:s1:1238568406257635402>" },
            { "s2", "<:s2:1238568405158858752>" },
            { "s3", "<:s3:1238568404085113024>" },
            { "s4", "<:s4:1238568403099451422>" },
            { "s5", "<:s5:1238568402197676133>" },
            { "s6", "<:s6:1238568401081733262>" },
            { "s7", "<:s7:1238568342466465832>" },
            { "s8", "<:s8:1238568303841247273>" },
            { "s9", "<:s9:1238568341531131935>" },
            { "s10", "<:s10:1238568301257293914>" },
            { "s11", "<:s11:1238568300280156320>" },
            { "s12", "<:s12:1238568299265134592>" },
            { "s13", "<:s13:1238568298317090897>" },
            { "s14", "<:s14:1238568297138622465>" },
            { "s15", "<:s15:1238568295439925371>" },
            { "s16", "<:s16:1238568296379580529>" },
            { "s17", "<:s17:1238568166498504824>" },
            { "s18", "<:s18:1238568213437223072>" },
            { "s19", "<:s19:1238568163327606814>" },
            { "s20", "<:s20:1238568155702497280>" },
            { "s21", "<:s21:1238568162375766167>" },
            { "s22", "<:s22:1238568161373327440>" },
            { "s23", "<:s23:1238568160060244079>" },
            { "s24", "<:s24:1238568159322050570>" },
            { "s25", "<:s25:1238568158449766531>" },
            { "s26", "<:s26:1238568156855926924>" },
            { "d1", "<:d1:1238582930926997724>" }, //
            { "d2", "<:d2:1238582928985034904>" }, //
            { "d3", "<:d3:1238582926292160614>" }, //
            { "d4", "<:d4:1238582925575065641>" }, //
            { "d5", "<:d5:1238569292165943296>" },
            { "d6", "<:d6:1238569328798863441>" },
            { "d7", "<:d7:1238569288617558017>" },
            { "d8", "<:d8:1238569287476711535>" },
            { "d9", "<:d9:1238569286331666553>" },
            { "d10", "<:d10:1238569285312315514>" },
            { "d11", "<:d11:1238569284356280360>" },
            { "d12", "<:d12:1238569283471151154>" },
            { "d13", "<:d13:1238569282363854919>" },
            { "d14", "<:d14:1238569281260617750>" },
            { "d15", "<:d15:1238568738165620828>" },
            { "d16", "<:d16:1238568698135056434>" },
            { "d17", "<:d17:1238582924551393371>" }, //
            { "d18", "<:d18:1238568695618474115>" },
            { "d19", "<:d19:1238568694154526732>" },
            { "d20", "<:d20:1238568693580042250>" },
            { "d21", "<:d21:1238568692099321917>" },
            { "d22", "<:d22:1238568691545673758>" },
            { "d23", "<:d23:1238568690711007292>" },
            { "d24", "<:d24:1238568689888919664>" },
            { "d25", "<:d25:1238568411915882497>" },
            { "d26", "<:d26:1238568456031436871>" },
            { "d27", "<:d27:1238568407813591051>" },
            { "d28", "<:d28:1238568407075655810>" },
            { "sp1", "<:s1:1238568406257635402>" },
            { "sp2", "<:s2:1238568405158858752>" },
            { "sp3", "<:s3:1238568404085113024>" },
            { "sp4", "<:s4:1238568403099451422>" },
            { "sp5", "<:s5:1238568402197676133>" },
            { "sp6", "<:s6:1238568401081733262>" },
            { "sp7", "<:s7:1238568342466465832>" },
            { "sp8", "<:s8:1238568303841247273>" },
            { "sp9", "<:s9:1238568341531131935>" },
            { "sp10", "<:s10:1238568301257293914>" },
            { "sp11", "<:s11:1238568300280156320>" },
            { "sp12", "<:s12:1238568299265134592>" },
            { "sp13", "<:s13:1238568298317090897>" },
            { "sp14", "<:s14:1238568297138622465>" },
            { "sp15", "<:s15:1238568295439925371>" },
            { "sp16", "<:s16:1238568296379580529>" },
            { "sp17", "<:s17:1238568166498504824>" },
            { "sp18", "<:s18:1238568213437223072>" },
            { "sp19", "<:s19:1238568163327606814>" },
            { "sp20", "<:s20:1238568155702497280>" },
            { "sp21", "<:s21:1238568162375766167>" },
            { "sp22", "<:s22:1238568161373327440>" },
            { "sp23", "<:s23:1238568160060244079>" },
            { "sp24", "<:s24:1238568159322050570>" },
            { "sp25", "<:s25:1238568158449766531>" },
            { "sp26", "<:s26:1238568156855926924>" },
            { "dp1", "<:d1:1238582930926997724>" }, //
            { "dp2", "<:d2:1238582928985034904>" }, //
            { "dp3", "<:d3:1238582926292160614>" }, //
            { "dp4", "<:d4:1238582925575065641>" }, //
            { "dp5", "<:d5:1238569292165943296>" },
            { "dp6", "<:d6:1238569328798863441>" },
            { "dp7", "<:d7:1238569288617558017>" },
            { "dp8", "<:d8:1238569287476711535>" },
            { "dp9", "<:d9:1238569286331666553>" },
            { "dp10", "<:d10:1238569285312315514>" },
            { "dp11", "<:d11:1238569284356280360>" },
            { "dp12", "<:d12:1238569283471151154>" },
            { "dp13", "<:d13:1238569282363854919>" },
            { "dp14", "<:d14:1238569281260617750>" },
            { "dp15", "<:d15:1238568738165620828>" },
            { "dp16", "<:d16:1238568698135056434>" },
            { "dp17", "<:d17:1238582924551393371>" }, //
            { "dp18", "<:d18:1238568695618474115>" },
            { "dp19", "<:d19:1238568694154526732>" },
            { "dp20", "<:d20:1238568693580042250>" },
            { "dp21", "<:d21:1238568692099321917>" },
            { "dp22", "<:d22:1238568691545673758>" },
            { "dp23", "<:d23:1238568690711007292>" },
            { "dp24", "<:d24:1238568689888919664>" },
            { "dp25", "<:d25:1238568411915882497>" },
            { "dp26", "<:d26:1238568456031436871>" },
            { "dp27", "<:d27:1238568407813591051>" },
            { "dp28", "<:d28:1238568407075655810>" },
            { "d29", "<:d29:1358437131667902617>" },
            { "coop2", "<:coop2:1238582935804842094>" }, //
            { "coop3", "<:coop3:1238582934185709621>" }, //
            { "coop4", "<:coop4:1238582932969361478>" }, //
            { "coop5", "<:coop5:1238582931858001991>" } //
        };

    /// <summary>
    ///     Swaps the emoji-token vocabulary (#LETTERGRADE|…#, #PLATE|…#, #DIFFICULTY|…#,
    ///     #MIX|…#) for the guild emojis — shared by the plain and rich send paths.
    /// </summary>
    private string ReplaceEmojiTokens(string message)
    {
        var replacedMessage = _letterGradeEmojis.Aggregate(message,
            (current, letterKv) => current.Replace($"#LETTERGRADE|{letterKv.Key}#", letterKv.Value,
                StringComparison.OrdinalIgnoreCase).Replace($"#LETTERGRADE|{letterKv.Key}|False#", letterKv.Value,
                StringComparison.OrdinalIgnoreCase));

        replacedMessage = _brokenLetterGradeEmojis.Aggregate(replacedMessage,
            (current, letterKv) => current.Replace($"#LETTERGRADE|{letterKv.Key}|True#", letterKv.Value,
                StringComparison.OrdinalIgnoreCase));

        replacedMessage = _plateEmojis.Aggregate(replacedMessage,
            (current, plateKv) => current.Replace($"#PLATE|{plateKv.Key}#", plateKv.Value,
                StringComparison.OrdinalIgnoreCase));

        replacedMessage = _difficultyShortHandEmojis.Aggregate(replacedMessage,
            (current, difficultyKv) => current.Replace($"#DIFFICULTY|{difficultyKv.Key}#", difficultyKv.Value,
                StringComparison.OrdinalIgnoreCase));

        replacedMessage = _mixEmojis.Aggregate(replacedMessage,
            (current, mixKv) => current.Replace($"#MIX|{mixKv.Key}#", mixKv.Value,
                StringComparison.OrdinalIgnoreCase));

        replacedMessage = replacedMessage.Replace("#LETTERGRADE|#", "");
        replacedMessage = replacedMessage.Replace("#PLATE|#", "");
        replacedMessage = replacedMessage.Replace("#DIFFICULTY|#", "");
        replacedMessage = replacedMessage.Replace("#MIX|#", "");
        return replacedMessage;
    }

    public async Task SendRichMessages(IEnumerable<RichBotMessage> messages, IEnumerable<ulong> channelIds,
        CancellationToken cancellationToken = default)
    {
        if (_client == null) throw new InvalidOperationException("Client was never started");
        var rendered = messages
            .Select(m => DiscordRichMessageRenderer.Render(m, ReplaceEmojiTokens))
            .ToArray();

        foreach (var channelId in channelIds)
            try
            {
                if (await _client.GetChannelAsync(channelId) is not IMessageChannel channel)
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

            foreach (var part in DiscordMessageSplitter.Split(ReplaceEmojiTokens(fallbackText)))
                await channel.SendMessageAsync(part);
        }
    }

    private async Task SendMessages(IEnumerable<string> messageEntities, IEnumerable<ulong> channelIds,
        Func<string, string> messageRetrieval,
        Action<string, IUserMessage>? process = default)
    {
        var replacedMessages = messageEntities.Select(ReplaceEmojiTokens).ToList();

        if (_client == null) throw new InvalidOperationException("Client was never started");
        foreach (var channelId in channelIds)
            try
            {
                if (await _client.GetChannelAsync(channelId) is not IMessageChannel channel)
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