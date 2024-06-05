using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Records;
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


            _botClient.WhenReady(async () =>
            {
                await _botClient.RegisterSlashCommand("calculate-score",
                    "Calculates a Phoenix Score based on score screen", "Calculating Score...",
                    async (channelId, userId, options) =>
                    {
                        var perfects = int.TryParse(options["perfects"], out var p) ? p : 0;
                        var greats = int.TryParse(options["greats"], out var g) ? g : 0;
                        var goods = int.TryParse(options["goods"], out var go) ? go : 0;
                        var bads = int.TryParse(options["bads"], out var b) ? b : 0;
                        var misses = int.TryParse(options["misses"], out var m) ? m : 0;
                        var combo = int.TryParse(options["combo"], out var c) ? c : 0;
                        double? calories = null;
                        if (options.TryGetValue("calories", out var calorieString) &&
                            double.TryParse(calorieString, out var cResult)) calories = cResult;
                        var screen = new ScoreScreen(perfects, greats, goods, bads, misses, combo, calories);
                        var loss = (double)(1000000 - screen.CalculatePhoenixScore);

                        if (!screen.IsValid)
                        {
                            await _botClient.SendMessage("This scoring configuration is invalid", channelId,
                                CancellationToken.None);
                        }
                        else
                        {
                            var message = $@"
{perfects:N0} Perfects, {greats:N0} Greats, {goods:N0} Goods, {bads:N0} Bads, {misses:N0} Misses, {combo:N0} Max Combo
**{(int)screen.CalculatePhoenixScore:N0} (#LETTERGRADE|{screen.LetterGrade}##PLATE|{screen.PlateText}#)**
{screen.NextLetterGrade()}
- {screen.GreatLoss:N0} Lost to Greats ({100.0 * screen.GreatLoss / loss:N2}%)
- {screen.GoodLoss:N0} Lost to Goods ({100.0 * screen.GoodLoss / loss:N2}%)
- {screen.BadLoss:N0} Lost to Bads ({100.0 * screen.BadLoss / loss:N2}%)
- {screen.MissLoss:N0} Lost to Misses ({100.0 * screen.MissLoss / loss:N2}%)
- {screen.ComboLoss:N0} Lost to Combo ({100.0 * screen.ComboLoss / loss:N2}%)";
                            if (screen.EstimatedSteps != null)
                                message += $@"
- {screen.EstimatedSteps:N0} Estimated Arrow Presses";
                            await _botClient.SendMessage(message
                                , channelId, CancellationToken.None);
                        }
                    },
                    new[]
                    {
                        ("perfects", "Perfect Count", true), ("greats", "Great Count", true),
                        ("goods", "Good Count", true),
                        ("bads", "Bad Count", true), ("misses", "Miss Count", true), ("combo", "Max Combo", true),
                        ("calories", "KCalories", false)
                    });
                await _botClient.RegisterSlashCommand("deregister-community-channel",
                    "De-Registers this channel to stop providing notifications for a community",
                    "De-Registering Channel...",
                    async (channelId, userId, options) =>
                    {
                        try
                        {
                            using var scope = _serviceCollection.CreateScope();

                            var token = new CancellationToken();
                            try
                            {
                                await scope.ServiceProvider.GetRequiredService<IMediator>()
                                    .Send(new RemoveDiscordChannelFromCommunityCommand(options["community-name"],
                                            channelId),
                                        token);
                            }
                            catch (CommunityNotFoundException)
                            {
                                await _botClient.SendMessages(
                                    new[] { "Community was not found or invite code is incorrect" },
                                    new[] { channelId },
                                    cancellationToken);
                            }
                            catch (InvalidOperationException e)
                            {
                                await _botClient.SendMessages(new[] { e.Message }, new[] { channelId },
                                    cancellationToken);
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(
                                $"There was an error while removing a channel for community: {e.Message} {e.StackTrace}",
                                e);

                            await _botClient.SendMessages(new[] { "An unknown error occurred" }, new[] { channelId },
                                cancellationToken);
                        }
                    }, new[]
                    {
                        ("community-name", "The name of the community", true)
                    });
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
                                                ? Guid.TryParse(inviteCodeString, out var inviteCode)
                                                    ? inviteCode
                                                    : null
                                                : null,
                                            channelId,
                                            options.TryGetValue("new-scores-notifications", out var sendScoresString) &&
                                            bool.TryParse(sendScoresString, out var sendScores) && sendScores,
                                            options.TryGetValue("new-member-notifications",
                                                out var sendNewMembersString) &&
                                            bool.TryParse(sendNewMembersString, out var sendNewMembers) &&
                                            sendNewMembers,
                                            options.TryGetValue("new-titles-notification",
                                                out var sendNewTitlesString) &&
                                            bool.TryParse(sendNewTitlesString, out var sendNewTitles) && sendNewTitles),
                                        token);
                            }
                            catch (CommunityNotFoundException)
                            {
                                await _botClient.SendMessages(
                                    new[] { "Community was not found or invite code is incorrect" },
                                    new[] { channelId },
                                    cancellationToken);
                            }
                            catch (InvalidOperationException e)
                            {
                                await _botClient.SendMessages(new[] { e.Message }, new[] { channelId },
                                    cancellationToken);
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(
                                $"There was an error while adding a channel for community: {e.Message} {e.StackTrace}",
                                e);

                            await _botClient.SendMessages(new[] { "An unknown error occurred" }, new[] { channelId },
                                cancellationToken);
                        }
                    }, new[]
                    {
                        ("community-name", "The name of the community (optional with invite code)", false),
                        ("invite-code",
                            "An active invite code for the community (not required for public communities)", false),
                        ("new-scores-notifications", "Set to True if you want notifications about new scores", true),
                        ("new-member-notifications",
                            "Set to True if you want notifications about new members joining the community", true),
                        ("new-titles-notifications", "Set to True if you want notifications about new titles achieved",
                            true)
                    });
            });
            _logger.LogInformation("Started bot client");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _botClient.Stop(cancellationToken);
            _logger.LogInformation("Stopped bot client");
        }
    }
}
