using MediatR;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.Randomizer.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
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
        private const string SiteBase = "https://piuscores.arroweclip.se";

        private readonly IBotClient _bot;
        private readonly ICommunityRepository _communities;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IDiscordFeedSubscriptionRepository _feeds;
        private readonly ILocalizedTextAccessor _localizer;
        private readonly IMediator _mediator;

        public BotCommandSaga(IBotClient bot, ICommunityRepository communities,
            IDiscordFeedSubscriptionRepository feeds, IMediator mediator, ICurrentUserAccessor currentUser,
            ILocalizedTextAccessor localizer)
        {
            _bot = bot;
            _communities = communities;
            _feeds = feeds;
            _mediator = mediator;
            _currentUser = currentUser;
            _localizer = localizer;
        }

        public Task<BotReply> Handle(HandleBotInteractionCommand request, CancellationToken cancellationToken)
        {
            var interaction = request.Interaction;
            var command = interaction.CommandPath.Count > 0 ? interaction.CommandPath[0] : string.Empty;
            return command switch
            {
                "calc" => Task.FromResult(Calc(interaction)),
                "chart" => ChartLookup(interaction, cancellationToken),
                "random" => RandomDraw(interaction, cancellationToken),
                "suggest" => Suggest(interaction, cancellationToken),
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
                "song" => await SongNameChoices(request.Request, cancellationToken),
                "preset" => await PresetChoices(request.Request, cancellationToken),
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

            var culture = ReadLanguage(interaction);
            await _feeds.Register(interaction.ChannelId, kind, mix, interaction.UserId, culture, cancellationToken);
            // The public confirmation doubles as the can-post probe and previews the
            // channel's chosen language.
            await _bot.SendMessage(
                FeedEmoji(kind) + " " + _localizer.Get(culture, "This channel now receives **{0} — {1}**. {2}",
                    _localizer.Get(culture, FeedName(kind)), mix.GetName(),
                    _localizer.Get(culture, FeedBlurb(kind))),
                interaction.ChannelId, cancellationToken);
            return new BotReply(Text: $"Done — this channel now receives {FeedName(kind)} for {mix.GetName()}.");
        }

        private async Task<BotReply> RegisterCommunity(BotInteraction interaction, CancellationToken cancellationToken)
        {
            var hasName = interaction.Options.TryGetValue("name", out var n) && !string.IsNullOrWhiteSpace(n);
            Guid? code = null;
            if (interaction.Options.TryGetValue("invite-code", out var c) && Guid.TryParse(c, out var parsed))
                code = parsed;
            if (!hasName && code == null)
                return new BotReply(Text: "Give a community name or an invite code.");

            try
            {
                await _mediator.Send(new AddDiscordChannelToCommunityCommand(
                        hasName ? Name.From(n) : null, code, interaction.ChannelId, ReadLanguage(interaction)),
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
            var communities = await _communities.GetChannelCommunities(interaction.ChannelId, cancellationToken);
            if (feeds.Count == 0 && communities.Count == 0)
                return new BotReply(Text:
                    "This channel isn't registered for anything yet. Use /piu register to add a feed.");

            var lines = feeds
                .OrderBy(f => f.Kind).ThenBy(f => f.Mix)
                .Select(f => $"{FeedEmoji(f.Kind)} {FeedName(f.Kind)} — **{f.Mix.GetName()}**{LanguageTag(f.Culture)}")
                .Concat(communities.Select(c => $"👥 Community — **{(string)c.Name}**{LanguageTag(c.Culture)}"))
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
            foreach (var community in await _communities.GetChannelCommunities(request.ChannelId, cancellationToken))
                choices.Add(new BotOptionChoice($"Community — {(string)community.Name}",
                    $"community:{(string)community.Name}"));
            return choices.Take(25).ToArray();
        }

        private async Task<BotReply> Suggest(BotInteraction interaction, CancellationToken cancellationToken)
        {
            var user = await ResolveUser(interaction.UserId, cancellationToken);
            if (user == null) return LinkNudge();
            _currentUser.SetScopedUser(user);

            var mix = ReadMix(interaction);
            var type = interaction.Options.TryGetValue("type", out var t) && Enum.TryParse<ChartType>(t, out var parsed)
                ? (ChartType?)parsed
                : null;
            var goal = interaction.Options.TryGetValue("goal", out var g) ? g : "TitleHunt";

            var recommendations = (await _mediator.Send(
                    new GetRecommendedChartsQuery(type, 0, mix, CategoriesForGoal(goal)), cancellationToken))
                .Take(6).ToList();
            if (recommendations.Count == 0)
                return new BotReply(Text:
                    "No suggestions right now — try a different goal, or import more scores first.");

            var charts = (await _mediator.Send(new GetChartsQuery(mix), cancellationToken)).ToDictionary(c => c.Id);
            var videos = await VideoLinks(recommendations.Select(r => r.ChartId), cancellationToken);
            var blocks = new List<IRichBotBlock> { new RichBotDivider() };
            foreach (var recommendation in recommendations)
            {
                if (!charts.TryGetValue(recommendation.ChartId, out var chart)) continue;
                var caption = string.IsNullOrWhiteSpace(recommendation.Explanation)
                    ? (string)recommendation.Category
                    : recommendation.Explanation;
                var video = videos.TryGetValue(chart.Id, out var url) ? $" · [Video]({url})" : string.Empty;
                blocks.Add(new RichBotSection(
                    $"#DIFFICULTY|{chart.DifficultyString}# [{(string)chart.Song.Name}]({SiteBase}/Chart/{chart.Id}){video}\n-# {caption}",
                    chart.Song.ImagePath));
            }

            return new BotReply(Card: new RichBotMessage(
                new RichBotSection($"### Suggested for you — {GoalName(goal)}\n-# {mix.GetName()} · based on your scores",
                    null),
                blocks,
                $"#MIX|{mix}# {mix.GetName()} · PIU Scores",
                mix.GetAccentColor(),
                new[] { new RichBotLink("Open Suggested Charts", new Uri(SiteBase)) }));
        }

        private static IReadOnlySet<RecommendationCategory> CategoriesForGoal(string goal) => goal switch
        {
            "ScorePush" => new HashSet<RecommendationCategory>
            {
                RecommendationCategory.PushPGs, RecommendationCategory.ImproveTop50,
                RecommendationCategory.RevisitOldScores
            },
            "FillGaps" => new HashSet<RecommendationCategory> { RecommendationCategory.FillScores },
            "PumbilityPush" => new HashSet<RecommendationCategory> { RecommendationCategory.PushPumbility },
            _ => new HashSet<RecommendationCategory>
            {
                RecommendationCategory.PushLevel, RecommendationCategory.SkillTitles
            }
        };

        private static string GoalName(string goal) => goal switch
        {
            "ScorePush" => "Score Push",
            "FillGaps" => "Fill Gaps",
            "PumbilityPush" => "Pumbility Push",
            _ => "Title Hunt"
        };

        private async Task<BotReply> RandomDraw(BotInteraction interaction, CancellationToken cancellationToken)
        {
            if (interaction.Options.TryGetValue("preset", out var presetName) && !string.IsNullOrWhiteSpace(presetName))
                return await RandomFromPreset(interaction, presetName.Trim(), cancellationToken);

            var mix = ReadMix(interaction);
            var count = interaction.Options.TryGetValue("count", out var cs) && int.TryParse(cs, out var c)
                ? Math.Clamp(c, 1, 10)
                : 3;
            var type = ReadType(interaction);
            var min = interaction.Options.TryGetValue("min-level", out var mn) && int.TryParse(mn, out var mnv)
                ? mnv
                : 1;
            var max = interaction.Options.TryGetValue("max-level", out var mx) && int.TryParse(mx, out var mxv)
                ? mxv
                : 29;
            if (min > max) (min, max) = (max, min);

            var drawn = (await _mediator.Send(new DrawRandomChartsQuery(BuildSettings(count, type, min, max), mix),
                cancellationToken)).ToList();
            if (drawn.Count == 0) return new BotReply(Text: "No charts matched those settings.");
            return await DrawCard(drawn, mix, $"Drew {drawn.Count} {(drawn.Count == 1 ? "chart" : "charts")}",
                DrawSubtitle(type, min, max, mix), cancellationToken);
        }

        private async Task<BotReply> RandomFromPreset(BotInteraction interaction, string presetName,
            CancellationToken cancellationToken)
        {
            var user = await ResolveUser(interaction.UserId, cancellationToken);
            if (user == null) return LinkNudge();
            _currentUser.SetScopedUser(user);

            var saved = (await _mediator.Send(new GetRandomSettingsQuery(), cancellationToken))
                .FirstOrDefault(s => string.Equals((string)s.SettingsName, presetName, StringComparison.OrdinalIgnoreCase));
            if (saved == null) return new BotReply(Text: $"You don't have a saved preset called \"{presetName}\".");

            var drawn = (await _mediator.Send(new DrawRandomChartsQuery(saved.Settings, saved.Mix), cancellationToken))
                .ToList();
            if (drawn.Count == 0) return new BotReply(Text: $"\"{presetName}\" didn't match any charts right now.");
            return await DrawCard(drawn, saved.Mix, $"Drew {drawn.Count} — {(string)saved.SettingsName}",
                "Your saved randomizer settings", cancellationToken);
        }

        private async Task<BotReply> DrawCard(IReadOnlyList<Chart> charts, MixEnum mix, string title, string subtitle,
            CancellationToken cancellationToken)
        {
            var drawn = charts.Take(10).ToList();
            var videos = await VideoLinks(drawn.Select(c => c.Id), cancellationToken);
            var blocks = new List<IRichBotBlock> { new RichBotDivider() };
            foreach (var chart in drawn)
            {
                var video = videos.TryGetValue(chart.Id, out var url) ? $" · [Video]({url})" : string.Empty;
                blocks.Add(new RichBotSection(
                    $"#DIFFICULTY|{chart.DifficultyString}# [{(string)chart.Song.Name}]({SiteBase}/Chart/{chart.Id}){video}",
                    chart.Song.ImagePath));
            }

            return new BotReply(Card: new RichBotMessage(
                new RichBotSection($"### {title}\n-# {subtitle}", null),
                blocks,
                $"#MIX|{mix}# {mix.GetName()} · PIU Scores",
                mix.GetAccentColor(),
                Array.Empty<RichBotLink>()));
        }

        private static RandomSettings BuildSettings(int count, ChartType type, int minLevel, int maxLevel)
        {
            var settings = new RandomSettings { Count = count, AllowRepeats = false };
            // Both a level weight and a song-type weight must be non-zero for a chart to be
            // eligible, so open every song category and weight the requested level band.
            settings.SongTypeWeights = settings.SongTypeWeights.ToDictionary(kv => kv.Key, kv => 1);
            switch (type)
            {
                case ChartType.Double:
                    SetLevelBand(settings.DoubleLevelWeights, minLevel, maxLevel);
                    break;
                case ChartType.CoOp:
                    settings.PlayerCountWeights = settings.PlayerCountWeights.ToDictionary(kv => kv.Key, kv => 1);
                    break;
                default:
                    SetLevelBand(settings.LevelWeights, minLevel, maxLevel);
                    break;
            }

            return settings;
        }

        private static void SetLevelBand(IDictionary<int, int> weights, int minLevel, int maxLevel)
        {
            foreach (var key in weights.Keys.ToList())
                weights[key] = key >= minLevel && key <= maxLevel ? 1 : 0;
        }

        private static string DrawSubtitle(ChartType type, int minLevel, int maxLevel, MixEnum mix)
        {
            var typeName = type switch
            {
                ChartType.Double => "Doubles",
                ChartType.CoOp => "Co-op",
                _ => "Singles"
            };
            return type == ChartType.CoOp
                ? $"{typeName} · {mix.GetName()}"
                : $"{typeName} · levels {minLevel}–{maxLevel} · {mix.GetName()}";
        }

        private static ChartType ReadType(BotInteraction interaction) =>
            interaction.Options.TryGetValue("type", out var value) && Enum.TryParse<ChartType>(value, out var type)
                ? type
                : ChartType.Single;

        private async Task<User?> ResolveUser(ulong discordUserId, CancellationToken cancellationToken) =>
            await _mediator.Send(new GetUserByExternalLoginQuery(discordUserId.ToString(), "Discord"),
                cancellationToken);

        private static BotReply LinkNudge() =>
            new(Text: "Link your Discord account first — sign in at https://piuscores.arroweclip.se and connect " +
                      "Discord on your Account page, then try again.");

        private async Task<IReadOnlyList<BotOptionChoice>> PresetChoices(BotAutocompleteRequest request,
            CancellationToken cancellationToken)
        {
            var user = await ResolveUser(request.UserId, cancellationToken);
            if (user == null) return Array.Empty<BotOptionChoice>();
            _currentUser.SetScopedUser(user);

            var partial = request.PartialValue?.Trim() ?? string.Empty;
            var saved = await _mediator.Send(new GetRandomSettingsQuery(), cancellationToken);
            return saved.Select(s => (string)s.SettingsName)
                .Where(name => partial.Length == 0 || name.Contains(partial, StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name)
                .Take(25)
                .Select(name => new BotOptionChoice(name, name))
                .ToArray();
        }

        private async Task<BotReply> ChartLookup(BotInteraction interaction, CancellationToken cancellationToken)
        {
            var mix = ReadMix(interaction);
            var query = interaction.Options.TryGetValue("song", out var s) ? s.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(query)) return new BotReply(Text: "Give a song name.");

            var all = (await _mediator.Send(new GetChartsQuery(mix), cancellationToken)).ToList();

            // Picked from autocomplete → a specific chart → the detailed card. Free text →
            // the song's difficulty list, which lets the user then pick one.
            if (Guid.TryParse(query, out var chartId))
            {
                var chart = all.FirstOrDefault(c => c.Id == chartId);
                if (chart != null) return await ChartDetailCard(chart, mix, all, cancellationToken);
            }

            var songName = all.Select(c => (string)c.Song.Name)
                                 .FirstOrDefault(name => string.Equals(name, query, StringComparison.OrdinalIgnoreCase))
                             ?? all.Select(c => (string)c.Song.Name).Distinct()
                                 .Where(name => name.Contains(query, StringComparison.OrdinalIgnoreCase))
                                 .OrderBy(name => name.Length).FirstOrDefault();
            if (songName == null)
                return new BotReply(Text: $"No chart found for \"{query}\" on {mix.GetName()}.");

            var charts = all.Where(c => (string)c.Song.Name == songName)
                .OrderBy(c => c.Type).ThenBy(c => (int)c.Level).ToList();
            var song = charts[0].Song;
            var subtitle = song.Bpm != null
                ? $"{(string)song.Artist} · {song.Bpm} BPM · {mix.GetName()}"
                : $"{(string)song.Artist} · {mix.GetName()}";
            var rows = string.Join("\n", charts.Select(c =>
                $"#DIFFICULTY|{c.DifficultyString}# [{c.DifficultyDisplay}]({SiteBase}/Chart/{c.Id})"));

            var card = new RichBotMessage(
                new RichBotSection($"### {songName}\n-# {subtitle}", song.ImagePath),
                new IRichBotBlock[]
                {
                    new RichBotDivider(),
                    new RichBotText(rows),
                    new RichBotText("-# Pick a difficulty from the list for its breakdown and similar charts.")
                },
                $"#MIX|{mix}# {mix.GetName()} · PIU Scores",
                mix.GetAccentColor(),
                Array.Empty<RichBotLink>());
            return new BotReply(Card: card);
        }

        // The chart-details card: what the /Charts page shows, sized for Discord — the
        // difficulty breakdown (scoring level + pass tier), the skill fingerprint, and
        // similar charts by skill. Each section drops out when its data is absent (the
        // similarity graph is empty until recalculate-chart-similarity runs).
        private async Task<BotReply> ChartDetailCard(Chart chart, MixEnum mix, IReadOnlyList<Chart> all,
            CancellationToken cancellationToken)
        {
            var song = chart.Song;
            var subtitle = song.Bpm != null
                ? $"{(string)song.Artist} · {song.Bpm} BPM · {mix.GetName()}"
                : $"{(string)song.Artist} · {mix.GetName()}";
            var blocks = new List<IRichBotBlock>
            {
                new RichBotDivider()
            };

            var scoringLevels = await _mediator.Send(new GetChartScoringLevelsQuery(mix), cancellationToken);
            var passTier = (await _mediator.Send(new GetTierListQuery(Name.From("Pass Count"), mix), cancellationToken))
                .Where(e => e.ChartId == chart.Id).Select(e => (TierListCategory?)e.Category).FirstOrDefault();
            var difficulty = new List<string>();
            if (scoringLevels.TryGetValue(chart.Id, out var scoringLevel))
                difficulty.Add($"Scoring level **{scoringLevel:0.0}** (listed {(int)chart.Level})");
            if (passTier != null && passTier != TierListCategory.Unrecorded)
                difficulty.Add($"Pass **{PassTierName(passTier.Value)}**");
            if (difficulty.Count > 0)
                blocks.Add(new RichBotText("📊 " + string.Join(" · ", difficulty)));

            // The real piucenter step-analysis skills (raw vocabulary), not the derived Skill
            // enum — that generic mapping is being recalibrated.
            var analysis = await _mediator.Send(new GetChartStepAnalysisQuery(chart.Id), cancellationToken);
            if (analysis != null && analysis.TopSkills.Count > 0)
                blocks.Add(new RichBotText("🎯 " + string.Join(" · ", analysis.TopSkills.Take(5).Select(PrettySkill))));

            var byId = all.ToDictionary(c => c.Id);
            var similar = (await _mediator.Send(new GetSimilarChartsQuery(chart.Id, mix), cancellationToken))
                .Where(r => byId.ContainsKey(r.ChartId))
                .Take(5)
                .ToList();
            if (similar.Count > 0)
            {
                blocks.Add(new RichBotDivider());
                blocks.Add(new RichBotText("**Similar charts**\n" + string.Join("\n", similar.Select(r =>
                {
                    var neighbor = byId[r.ChartId];
                    return $"#DIFFICULTY|{neighbor.DifficultyString}# [{(string)neighbor.Song.Name}]({SiteBase}/Chart/{neighbor.Id}) — {(int)Math.Round(r.Score * 100)}%";
                }))));
            }

            return new BotReply(Card: new RichBotMessage(
                new RichBotSection($"### {(string)song.Name} — {chart.DifficultyDisplay}\n-# {subtitle}", song.ImagePath),
                blocks,
                $"#MIX|{mix}# {mix.GetName()} · PIU Scores",
                mix.GetAccentColor(),
                new[] { new RichBotLink("Open chart page", new Uri($"{SiteBase}/Chart/{chart.Id}")) }));
        }

        private static string PassTierName(TierListCategory category) => category switch
        {
            TierListCategory.VeryEasy => "Very Easy",
            TierListCategory.VeryHard => "Very Hard",
            _ => category.ToString()
        };

        // piucenter badges are raw underscore-separated names; make them human without
        // pretending they're the display Skill vocabulary.
        private static string PrettySkill(string raw) =>
            string.Join(" ", raw.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]));

        private async Task<IReadOnlyDictionary<Guid, Uri>> VideoLinks(IEnumerable<Guid> chartIds,
            CancellationToken cancellationToken)
        {
            var ids = chartIds.Distinct().ToArray();
            if (ids.Length == 0) return new Dictionary<Guid, Uri>();
            var videos = await _mediator.Send(new GetChartVideosQuery(ids), cancellationToken);
            return videos.GroupBy(v => v.ChartId).ToDictionary(g => g.Key, g => g.First().VideoUrl);
        }

        private async Task<IReadOnlyList<BotOptionChoice>> SongNameChoices(BotAutocompleteRequest request,
            CancellationToken cancellationToken)
        {
            var mix = request.Options.TryGetValue("mix", out var m) && Enum.TryParse<MixEnum>(m, out var parsed)
                ? parsed
                : MixEnum.Phoenix2;
            var partial = request.PartialValue?.Trim() ?? string.Empty;
            var all = await _mediator.Send(new GetChartsQuery(mix), cancellationToken);
            // One entry per chart ("Ugly Dee S20"), value = the chart id, so a pick lands on a
            // specific chart for the detailed card; matching mirrors the site's ChartSelector.
            return all
                .Where(c => partial.Length == 0 ||
                            $"{(string)c.Song.Name} {c.DifficultyDisplay}".Contains(partial,
                                StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => (string)c.Song.Name).ThenBy(c => c.Type).ThenBy(c => (int)c.Level)
                .Take(25)
                .Select(c => new BotOptionChoice($"{(string)c.Song.Name} {c.DifficultyDisplay}", c.Id.ToString()))
                .ToArray();
        }

        private static MixEnum ReadMix(BotInteraction interaction) =>
            interaction.Options.TryGetValue("mix", out var value) && Enum.TryParse<MixEnum>(value, out var mix)
                ? mix
                : MixEnum.Phoenix2;

        // The registration's chosen post language; absent or unrecognized = null = English.
        private static string? ReadLanguage(BotInteraction interaction) =>
            SupportedCultures.NormalizeOrNull(
                interaction.Options.TryGetValue("language", out var value) ? value : null);

        // Feed-list suffix showing a registration's non-default language by its native name.
        private static string LanguageTag(string? culture) =>
            culture == null ? string.Empty : $" · {SupportedCultures.NativeNameFor(culture)}";

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
