using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Clients;

public sealed class DiscordBotClient : IBotClient
{
    private static readonly ulong[] ChannelIds = { 1009932033365127168 };
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

    private async Task SendMessage(IEnumerable<ulong> channelIds,
        string message)
    {
        if (_client == null) throw new Exception("Client was never started");
        foreach (var channelId in channelIds)
        {
            if (await _client.GetChannelAsync(channelId) is not IMessageChannel channel)
            {
                _logger.LogWarning($"Channel {channelId} was not found");
                continue;
            }

            var userMessage = await channel.SendMessageAsync(message);
        }
    }

    public async Task SendMessageToUser(ulong userId, string message, CancellationToken cancellationToken = default)
    {
        var user = await GetUser(userId);

        await user.SendMessageAsync(message);
    }

    public async Task SendMessages(IEnumerable<string> messages, IEnumerable<ulong> channelIds,
        CancellationToken cancellationToken = default)
    {
        await SendMessages(messages, channelIds, m => m);
    }


    public void RegisterReactRemoved(Func<string, ulong, ulong, Task> execution)
    {
        if (_client == null) throw new Exception("Bot was not initialized");
        _client.ReactionRemoved += async (message, channel, reaction) =>
        {
            await execution(reaction.Emote.ToString() ?? string.Empty, reaction.UserId, reaction.MessageId);
        };
    }

    public void RegisterReactAdded(Func<string, ulong, ulong, Task> execution)
    {
        if (_client == null) throw new Exception("Bot was not initialized");
        _client.ReactionAdded += async (message, channel, reaction) =>
        {
            await execution(reaction.Emote.ToString() ?? string.Empty, reaction.UserId, reaction.MessageId);
        };
    }

    public async Task SendFileToUser(ulong userId, Stream fileStream, string fileName, string? message = null,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUser(userId);

        await user.SendFileAsync(fileStream, fileName, message);
    }

    public async Task RegisterSlashCommand(string name, string description, string response,
        Func<ulong, Task> execution)
    {
        await RegisterSlashCommand(name, description, response, o => { },
            async command => await execution(command.Channel.Id));
    }

    public async Task RegisterSlashCommand(string name, string description, string response,
        Func<ulong, ulong, IDictionary<string, string>, Task> execution,
        IEnumerable<(string name, string description, bool isRequired)> options,
        bool requireChannelAdmin = false)
    {
        await RegisterSlashCommand(name, description, response, builder =>
        {
            if (requireChannelAdmin) builder.DefaultMemberPermissions = GuildPermission.ManageChannels;
            foreach (var option in options)
            {
                var optionBuilder = new SlashCommandOptionBuilder()
                    .WithName(option.name)
                    .WithDescription(option.description)
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(option.isRequired);
                builder.AddOption(optionBuilder);
            }
        }, async command =>
        {
            await execution(command.Channel.Id, command.User.Id,
                command.Data.Options.ToDictionary(o => o.Name, o => o.Value.ToString() ?? string.Empty));
        });
    }

    public async Task RegisterMenuSlashCommand(string name, string description, string response,
        IEnumerable<(string label, string url)> menuButtons)
    {
        await RegisterSlashCommand(name, description, response, c => { }, _ => Task.CompletedTask,
            builder =>
            {
                foreach (var button in menuButtons)
                    builder.WithButton(button.label,
                        style: ButtonStyle.Link, url: button.url);
            });
    }

    private async Task RegisterSlashCommand(string name, string description, string response,
        Action<SlashCommandBuilder> builderOptions,
        Func<SocketSlashCommand, Task> execution,
        Action<ComponentBuilder>? componentOptions = null)
    {
        if (_client == null) throw new Exception("Discord client was not started");
        var builder = new SlashCommandBuilder()
            .WithName(name)
            .WithDescription(description);
        builderOptions(builder);

        try
        {
            await _client.CreateGlobalApplicationCommandAsync(builder.Build());
            _client.SlashCommandExecuted += async command =>
            {
                if (command.CommandName == name)
                    try
                    {
                        ComponentBuilder? componentBuilder = null;
                        if (componentOptions != null)
                        {
                            componentBuilder = new ComponentBuilder();
                            componentOptions(componentBuilder);
                        }

                        await command.RespondAsync(response, components: componentBuilder?.Build());
                        await execution(command);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(
                            $"An exception while executing the command for {command.CommandName}: {e.Message} {e.StackTrace}",
                            e);
                    }
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"Error when registering the slash command {name}", e);
        }
    }

    private async Task<IUser> GetUser(ulong userId)
    {
        if (_client == null) throw new Exception("Client was never started");

        var user = await _client.GetUserAsync(userId);
        if (user == null) throw new Exception($"User {userId} was not found when sending a message");

        return user;
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
            { "coop2", "<:coop2:1238582935804842094>" }, //
            { "coop3", "<:coop3:1238582934185709621>" }, //
            { "coop4", "<:coop4:1238582932969361478>" }, //
            { "coop5", "<:coop5:1238582931858001991>" } //
        };

    private async Task SendMessages(IEnumerable<string> messageEntities, IEnumerable<ulong> channelIds,
        Func<string, string> messageRetrieval,
        Action<string, IUserMessage>? process = default)
    {
        var replacedMessages = new List<string>();
        foreach (var message in messageEntities)
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

            replacedMessage = replacedMessage.Replace("#LETTERGRADE|#", "");
            replacedMessage = replacedMessage.Replace("#PLATE|#", "");
            replacedMessage = replacedMessage.Replace("#DIFFICULTY|#", "");
            replacedMessages.Add(replacedMessage);
        }

        if (_client == null) throw new Exception("Client was never started");
        foreach (var channelId in channelIds)
            try
            {
                if (await _client.GetChannelAsync(channelId) is not IMessageChannel channel)
                {
                    _logger.LogWarning($"Channel {channelId} was not found");
                    continue;
                }

                foreach (var message in replacedMessages)
                    try
                    {
                        var userMessage = await channel.SendMessageAsync(messageRetrieval(message));
                        if (process != null) process(message, userMessage);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Could not send message to channel {channelId}. Message :{message}", ex);
                    }
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Could not send messages to channel {channelId}.", e);
            }
    }
}