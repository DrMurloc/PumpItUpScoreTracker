using MediatR;
using Microsoft.Extensions.Options;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Web.HostedServices
{
    public sealed class BotHostedService : IHostedService
    {
        private readonly IBotClient _botClient;
        private readonly IOptions<DiscordConfiguration> _discordConfig;
        private readonly ILogger<BotHostedService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private bool _started;

        public BotHostedService(IBotClient botClient,
            ILogger<BotHostedService> logger,
            IServiceProvider serviceProvider,
            IOptions<DiscordConfiguration> discordConfig)
        {
            _botClient = botClient;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _discordConfig = discordConfig;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Local dev runs without a Discord bot token — skip the bot entirely instead
            // of failing startup on a connection the environment can't make.
            if (string.IsNullOrWhiteSpace(_discordConfig.Value.BotToken))
            {
                _logger.LogWarning("Discord bot token not configured — bot disabled for this run.");
                return;
            }

            _started = true;
            await _botClient.Start(cancellationToken);

            _botClient.WhenReady(async () =>
                await _botClient.RegisterCommands(PiuCommandCatalog.Commands, OnInteraction, OnAutocomplete));

            _logger.LogInformation("Started bot client");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (!_started) return;

            await _botClient.Stop(cancellationToken);
            _logger.LogInformation("Stopped bot client");
        }

        // The bot host owns nothing but the dispatch: every /piu reply is composed by the
        // Communities router behind IMediator, in its own DI scope per interaction.
        private async Task<BotReply> OnInteraction(BotInteraction interaction)
        {
            using var scope = _serviceProvider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new HandleBotInteractionCommand(interaction));
        }

        private async Task<IReadOnlyList<BotOptionChoice>> OnAutocomplete(BotAutocompleteRequest request)
        {
            using var scope = _serviceProvider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new GetBotAutocompleteQuery(request));
        }
    }
}
