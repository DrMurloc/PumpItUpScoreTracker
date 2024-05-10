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
        IEnumerable<(string name, string description)> options)
    {
        await RegisterSlashCommand(name, description, response, builder =>
        {
            foreach (var option in options)
            {
                var optionBuilder = new SlashCommandOptionBuilder()
                    .WithName(option.name)
                    .WithDescription(option.description)
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true);
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

    private async Task SendMessages(IEnumerable<string> messageEntities, IEnumerable<ulong> channelIds,
        Func<string, string> messageRetrieval,
        Action<string, IUserMessage>? process = default)
    {
        var replacedMessages = new List<string>();
        foreach (var message in messageEntities)
        {
            var replacedMessage = _letterGradeEmojis.Aggregate(message,
                (current, letterKv) => current.Replace($"#LETTERGRADE|{letterKv.Key}#", letterKv.Value));

            replacedMessage = _plateEmojis.Aggregate(replacedMessage,
                (current, plateKv) => current.Replace($"#PLATE|{plateKv.Key}#", plateKv.Value));

            replacedMessage = replacedMessage.Replace("#LETTERGRADE|#", "");
            replacedMessage = replacedMessage.Replace("#PLATE|#", "");
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