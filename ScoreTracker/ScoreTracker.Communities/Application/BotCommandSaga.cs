using MediatR;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Application
{
    /// <summary>
    ///     Routes every /piu slash-command invocation and autocomplete lookup. Feature-grouped
    ///     like the other sagas: one class owns the whole command surface, dispatching to the
    ///     other verticals through their published contracts. Composition lives here (not in the
    ///     bot host) so every reply is testable in the fast component suite.
    /// </summary>
    internal sealed class BotCommandSaga :
        IRequestHandler<HandleBotInteractionCommand, BotReply>,
        IRequestHandler<GetBotAutocompleteQuery, IReadOnlyList<BotOptionChoice>>
    {
        private readonly IBotClient _bot;
        private readonly ICommunityRepository _communities;
        private readonly IDiscordFeedSubscriptionRepository _feeds;
        private readonly IMediator _mediator;

        public BotCommandSaga(IBotClient bot, ICommunityRepository communities,
            IDiscordFeedSubscriptionRepository feeds, IMediator mediator)
        {
            _bot = bot;
            _communities = communities;
            _feeds = feeds;
            _mediator = mediator;
        }

        public Task<BotReply> Handle(HandleBotInteractionCommand request, CancellationToken cancellationToken)
        {
            var interaction = request.Interaction;
            var command = interaction.CommandPath.Count > 0 ? interaction.CommandPath[0] : string.Empty;
            return command switch
            {
                "calc" => Task.FromResult(Calc(interaction)),
                "register" => Register(interaction, cancellationToken),
                "unregister" => Unregister(interaction, cancellationToken),
                "feeds" => Feeds(interaction, cancellationToken),
                _ => Task.FromResult(new BotReply(Text: "That command isn't available yet."))
            };
        }

        public async Task<IReadOnlyList<BotOptionChoice>> Handle(GetBotAutocompleteQuery request,
            CancellationToken cancellationToken)
        {
            var focused = request.Request.FocusedOptionName;
            return focused switch
            {
                "name" => await CommunityNameChoices(request.Request, cancellationToken),
                "feed" => await FeedChoices(request.Request, cancellationToken),
                _ => Array.Empty<BotOptionChoice>()
            };
        }

        private async Task<BotReply> Register(BotInteraction interaction, CancellationToken cancellationToken)
        {
            if (!interaction.InvokerCanManageChannels) return Deny();
            if (!await _bot.CanPostToChannel(interaction.ChannelId, cancellationToken)) return CannotPost();

            var sub = interaction.CommandPath.Count > 1 ? interaction.CommandPath[1] : string.Empty;
            if (sub == "community") return await RegisterCommunity(interaction, cancellationToken);

            if (!TryFeedKind(sub, out var kind)) return new BotReply(Text: "Unknown feed.");
            if (!TryMix(interaction, out var mix)) return new BotReply(Text: "Pick a mix.");

            await _feeds.Register(interaction.ChannelId, kind, mix, interaction.UserId, cancellationToken);
            await _bot.SendMessage(
                $"{FeedEmoji(kind)} This channel now receives **{FeedName(kind)} — {mix.GetName()}**. {FeedBlurb(kind)}",
                interaction.ChannelId, cancellationToken);
            return new BotReply(Text: $"Done — this channel now receives {FeedName(kind)} for {mix.GetName()}.");
        }

        private async Task<BotReply> RegisterCommunity(BotInteraction interaction, CancellationToken cancellationToken)
        {
            Name? name = interaction.Options.TryGetValue("name", out var n) && !string.IsNullOrWhiteSpace(n)
                ? Name.From(n)
                : null;
            Guid? code = interaction.Options.TryGetValue("invite-code", out var c) && Guid.TryParse(c, out var parsed)
                ? parsed
                : null;
            if (name == null && code == null)
                return new BotReply(Text: "Give a community name or an invite code.");

            try
            {
                await _mediator.Send(new AddDiscordChannelToCommunityCommand(name, code, interaction.ChannelId),
                    cancellationToken);
                return new BotReply(Text: "Done — this channel is registered for that community.");
            }
            catch (CommunityNotFoundException)
            {
                return new BotReply(Text: "That community wasn't found, or the invite code is wrong.");
            }
            catch (InvalidOperationException e)
            {
                return new BotReply(Text: e.Message);
            }
        }

        private async Task<BotReply> Unregister(BotInteraction interaction, CancellationToken cancellationToken)
        {
            if (!interaction.InvokerCanManageChannels) return Deny();

            var value = interaction.Options.TryGetValue("feed", out var v) ? v : string.Empty;
            if (value.StartsWith("feed:", StringComparison.Ordinal))
            {
                var parts = value.Split(':');
                if (parts.Length == 3 && Enum.TryParse<DiscordFeedKind>(parts[1], out var kind) &&
                    Enum.TryParse<MixEnum>(parts[2], out var mix))
                {
                    var removed = await _feeds.Unregister(interaction.ChannelId, kind, mix, cancellationToken);
                    return new BotReply(Text: removed
                        ? $"Removed {FeedName(kind)} for {mix.GetName()} from this channel."
                        : "That feed wasn't registered here.");
                }
            }
            else if (value.StartsWith("community:", StringComparison.Ordinal))
            {
                var name = value["community:".Length..];
                try
                {
                    await _mediator.Send(
                        new RemoveDiscordChannelFromCommunityCommand(Name.From(name), interaction.ChannelId),
                        cancellationToken);
                    return new BotReply(Text: $"Removed the {name} community feed from this channel.");
                }
                catch (CommunityNotFoundException)
                {
                    return new BotReply(Text: "That community registration wasn't found.");
                }
            }

            return new BotReply(Text: "Pick a registration to remove from the list.");
        }

        private async Task<BotReply> Feeds(BotInteraction interaction, CancellationToken cancellationToken)
        {
            var feeds = await _feeds.GetForChannel(interaction.ChannelId, cancellationToken);
            var communities = await _communities.GetChannelCommunityNames(interaction.ChannelId, cancellationToken);
            if (feeds.Count == 0 && communities.Count == 0)
                return new BotReply(Text:
                    "This channel isn't registered for anything yet. Use /piu register to add a feed.");

            var lines = feeds
                .OrderBy(f => f.Kind).ThenBy(f => f.Mix)
                .Select(f => $"{FeedEmoji(f.Kind)} {FeedName(f.Kind)} — **{f.Mix.GetName()}**")
                .Concat(communities.Select(name => $"👥 Community — **{(string)name}**"))
                .ToList();

            var card = new RichBotMessage(
                new RichBotSection("### This channel receives", null),
                new IRichBotBlock[] { new RichBotDivider(), new RichBotText(string.Join("\n", lines)) },
                "Remove one with /piu unregister",
                null,
                Array.Empty<RichBotLink>());
            return new BotReply(Card: card);
        }

        private async Task<IReadOnlyList<BotOptionChoice>> CommunityNameChoices(BotAutocompleteRequest request,
            CancellationToken cancellationToken)
        {
            var partial = request.PartialValue?.Trim() ?? string.Empty;
            var communities = await _communities.GetPublicCommunities(cancellationToken);
            return communities
                .Select(c => (string)c.CommunityName)
                .Where(name => name.Contains(partial, StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name)
                .Take(25)
                .Select(name => new BotOptionChoice(name, name))
                .ToArray();
        }

        private async Task<IReadOnlyList<BotOptionChoice>> FeedChoices(BotAutocompleteRequest request,
            CancellationToken cancellationToken)
        {
            var choices = new List<BotOptionChoice>();
            foreach (var f in await _feeds.GetForChannel(request.ChannelId, cancellationToken))
                choices.Add(new BotOptionChoice($"{FeedName(f.Kind)} — {f.Mix.GetName()}",
                    $"feed:{f.Kind}:{f.Mix}"));
            foreach (var name in await _communities.GetChannelCommunityNames(request.ChannelId, cancellationToken))
                choices.Add(new BotOptionChoice($"Community — {(string)name}", $"community:{(string)name}"));
            return choices.Take(25).ToArray();
        }

        private static BotReply Calc(BotInteraction interaction)
        {
            var perfects = ReadInt(interaction, "perfects");
            var greats = ReadInt(interaction, "greats");
            var goods = ReadInt(interaction, "goods");
            var bads = ReadInt(interaction, "bads");
            var misses = ReadInt(interaction, "misses");
            var combo = ReadInt(interaction, "combo");
            double? calories = interaction.Options.TryGetValue("calories", out var calorieString) &&
                               double.TryParse(calorieString, out var parsed)
                ? parsed
                : null;

            var screen = new ScoreScreen(perfects, greats, goods, bads, misses, combo, calories);
            if (!screen.IsValid)
                return new BotReply(Text: "That scoring configuration is invalid.");

            var loss = (double)(1000000 - screen.CalculatePhoenixScore);
            var message = $@"{perfects:N0} Perfects, {greats:N0} Greats, {goods:N0} Goods, {bads:N0} Bads, {misses:N0} Misses, {combo:N0} Max Combo
**{(int)screen.CalculatePhoenixScore:N0} (#LETTERGRADE|{screen.LetterGrade}##PLATE|{screen.PlateText}#)**
{screen.NextLetterGrade()}
- {screen.GreatLoss:N0} Lost to Greats ({SafePercent(screen.GreatLoss, loss)}%)
- {screen.GoodLoss:N0} Lost to Goods ({SafePercent(screen.GoodLoss, loss)}%)
- {screen.BadLoss:N0} Lost to Bads ({SafePercent(screen.BadLoss, loss)}%)
- {screen.MissLoss:N0} Lost to Misses ({SafePercent(screen.MissLoss, loss)}%)
- {screen.ComboLoss:N0} Lost to Combo ({SafePercent(screen.ComboLoss, loss)}%)";
            if (screen.EstimatedSteps != null)
                message += $"{Environment.NewLine}- {screen.EstimatedSteps:N0} Estimated Arrow Presses";

            return new BotReply(Text: message);
        }

        private static BotReply Deny() =>
            new(Text: "You need the Manage Channels permission in this server to do that.");

        private static BotReply CannotPost() =>
            new(Text: "I can't post in this channel yet — give me the View Channel and Send Messages " +
                      "permissions here, then run the command again.");

        private static bool TryFeedKind(string sub, out DiscordFeedKind kind)
        {
            switch (sub)
            {
                case "weekly": kind = DiscordFeedKind.WeeklyCharts; return true;
                case "daily": kind = DiscordFeedKind.DailyStep; return true;
                case "official": kind = DiscordFeedKind.OfficialLeaderboards; return true;
                default: kind = default; return false;
            }
        }

        private static bool TryMix(BotInteraction interaction, out MixEnum mix)
        {
            mix = MixEnum.Phoenix;
            return interaction.Options.TryGetValue("mix", out var value) && Enum.TryParse(value, out mix);
        }

        private static string FeedName(DiscordFeedKind kind) => kind switch
        {
            DiscordFeedKind.WeeklyCharts => "Weekly Charts",
            DiscordFeedKind.DailyStep => "Daily Step",
            DiscordFeedKind.OfficialLeaderboards => "Official Leaderboards",
            _ => kind.ToString()
        };

        private static string FeedEmoji(DiscordFeedKind kind) => kind switch
        {
            DiscordFeedKind.WeeklyCharts => "📅",
            DiscordFeedKind.DailyStep => "☀️",
            DiscordFeedKind.OfficialLeaderboards => "🌍",
            _ => "📣"
        };

        private static string FeedBlurb(DiscordFeedKind kind) => kind switch
        {
            DiscordFeedKind.WeeklyCharts => "Results and the new lineup post when the board rotates each Monday.",
            DiscordFeedKind.DailyStep => "Yesterday's board and today's chart post each day.",
            DiscordFeedKind.OfficialLeaderboards => "The digest posts after the weekly Sunday sweep.",
            _ => string.Empty
        };

        private static string SafePercent(int part, double whole) =>
            whole <= 0 ? "0.00" : (100.0 * part / whole).ToString("N2");

        private static int ReadInt(BotInteraction interaction, string key) =>
            interaction.Options.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : 0;
    }
}
