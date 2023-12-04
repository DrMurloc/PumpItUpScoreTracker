using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Clients;

public sealed class DiscordBotClient : IBotClient
{
    private static readonly ulong[] ChannelIds = { 1181340784751026186 };
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

    public async Task PublishQualifiersMessage(string message, CancellationToken cancellationToken)
    {
        await SendMessage(ChannelIds, message);
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
}