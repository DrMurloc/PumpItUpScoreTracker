using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Web.HostedServices
{
    public sealed class BotHostedService : IHostedService
    {
        private readonly IBotClient _botClient;
        private readonly ILogger<BotHostedService> _logger;
        private readonly IServiceProvider _serviceCollection;

        public BotHostedService(IBotClient botClient,
            ILogger<BotHostedService> logger,
            IServiceProvider serviceCollection)
        {
            _botClient = botClient;
            _logger = logger;
            _serviceCollection = serviceCollection;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _botClient.Start(cancellationToken);

            await _botClient.RegisterSlashCommand("register-community-channel",
                "Registers this channel to provide notifications for a community", "Registering Channel...",
                async (channelId, userId, options) =>
                {
                    try
                    {
                        using var scope = _serviceCollection.CreateScope();

                        var token = new CancellationToken();
                        try
                        {
                            await scope.ServiceProvider.GetRequiredService<IMediator>()
                                .Send(new AddDiscordChannelToCommunityCommand(
                                        options.TryGetValue("community-name", out var name) ? name : null,
                                        options.TryGetValue("invite-code", out var inviteCodeString)
                                            ? Guid.TryParse(inviteCodeString, out var inviteCode) ? inviteCode : null
                                            : null,
                                        channelId,
                                        options.TryGetValue("new-scores-notifications", out var sendScoresString) &&
                                        bool.TryParse(sendScoresString, out var sendScores) && sendScores,
                                        options.TryGetValue("new-member-notifications", out var sendNewMembersString) &&
                                        bool.TryParse(sendNewMembersString, out var sendNewMembers) && sendNewMembers,
                                        options.TryGetValue("new-titles-notification", out var sendNewTitlesString) &&
                                        bool.TryParse(sendNewTitlesString, out var sendNewTitles) && sendNewTitles),
                                    token);
                        }
                        catch (CommunityNotFoundException)
                        {
                            await _botClient.SendMessages(
                                new[] { "Community was not found or invite code is incorrect" }, new[] { channelId },
                                cancellationToken);
                        }
                        catch (InvalidOperationException e)
                        {
                            await _botClient.SendMessages(new[] { e.Message }, new[] { channelId }, cancellationToken);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(
                            $"There was an error while adding a channel for community: {e.Message} {e.StackTrace}", e);

                        await _botClient.SendMessages(new[] { "An unknown error occurred" }, new[] { channelId },
                            cancellationToken);
                    }
                }, new[]
                {
                    ("community-name", "The name of the community (optional with invite code)"),
                    ("invite-code", "An active invite code for the community (not required for public communities)"),
                    ("new-scores-notifications", "Set to True if you want notifications about new scores"),
                    ("new-member-notifications",
                        "Set to True if you want notifications about new members joining the community"),
                    ("new-titles-notifications", "Set to True if you want notifications about new titles achieved")
                });
            _botClient.WhenReady(() => Task.CompletedTask);
            _logger.LogInformation("Started bot client");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _botClient.Stop(cancellationToken);
            _logger.LogInformation("Stopped bot client");
        }
    }
}
