using System.Globalization;
using MassTransit;
using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.WeeklyChallenge.Contracts;
using ScoreTracker.WeeklyChallenge.Contracts.Events;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;

namespace ScoreTracker.Communities.Application
{
    /// <summary>
    ///     Posts the opt-in broadcast feeds (weekly charts, daily step) to their subscribed
    ///     channels when a board rotates. Reads results through the owning verticals'
    ///     published contracts and fans out via IBotClient; a channel that is also
    ///     community-registered glows its members' rows.
    /// </summary>
    internal sealed class DiscordFeedSaga :
        IConsumer<WeeklyChartsRotatedEvent>,
        IConsumer<DailyStepRotatedEvent>
    {
        private const string SiteBase = "https://piuscores.arroweclip.se";
        private const int TopCharts = 5;
        private const int TopPlayers = 10;

        private readonly IBotClient _bot;
        private readonly ICommunityRepository _communities;
        private readonly IDiscordFeedSubscriptionRepository _feeds;
        private readonly ILocalizedTextAccessor _localizer;
        private readonly IMediator _mediator;
        private readonly IUserReader _users;

        public DiscordFeedSaga(IBotClient bot, IMediator mediator, IUserReader users,
            IDiscordFeedSubscriptionRepository feeds, ICommunityRepository communities,
            ILocalizedTextAccessor localizer)
        {
            _bot = bot;
            _mediator = mediator;
            _users = users;
            _feeds = feeds;
            _communities = communities;
            _localizer = localizer;
        }

        public async Task Consume(ConsumeContext<WeeklyChartsRotatedEvent> context)
        {
            var mix = context.Message.Mix;
            var ct = context.CancellationToken;
            var channels = await _feeds.GetSubscribedChannels(DiscordFeedKind.WeeklyCharts, mix, ct);
            if (channels.Count == 0) return;

            var dates = (await _mediator.Send(new GetPastWeeklyDatesQuery(mix), ct)).ToList();
            if (dates.Count == 0) return;
            var latest = dates.Max();

            var entries = (await _mediator.Send(new GetPastWeeklyEntriesQuery(latest, mix), ct)).ToList();
            var charts = (await _mediator.Send(new GetChartsQuery(mix), ct)).ToDictionary(c => c.Id);

            var ranked = entries
                .GroupBy(e => e.ChartId)
                .Where(g => charts.ContainsKey(g.Key))
                .Select(g => new RankedBoard(g.Key, g.Count(),
                    WeeklyChartSuggestionPolicy.ProcessIntoPlaces(g).Take(TopPlayers).ToList()))
                .OrderByDescending(r => r.PlayerCount)
                .ToList();
            if (ranked.Count == 0) return;

            var top = ranked.Take(TopCharts).ToList();
            var names = await ResolveNames(top.SelectMany(r => r.Placements.Select(p => p.Item2.UserId)), ct);
            var lineup = (await _mediator.Send(new GetWeeklyChartsQuery(mix), ct)).ToList();
            var lineupVideos = await VideoLinks(lineup.Select(w => w.ChartId), ct);

            // Data is gathered once; only the rendering repeats per channel, in each
            // channel's registered language.
            foreach (var channel in channels)
            {
                var labels = await CommunityLabelsFor(channel.ChannelId, ct);
                var cards = new List<RichBotMessage>();
                for (var i = 0; i < top.Count; i++)
                    cards.Add(WeeklyResultCard(mix, top[i], charts, names, labels, i + 1, top.Count,
                        ranked.Count - top.Count, latest, channel.Culture));
                cards.Add(LineupCard(mix, lineup, charts, lineupVideos, channel.Culture));
                await _bot.SendRichMessages(cards, new[] { channel.ChannelId }, ct);
            }
        }

        public async Task Consume(ConsumeContext<DailyStepRotatedEvent> context)
        {
            var msg = context.Message;
            var ct = context.CancellationToken;
            var channels = await _feeds.GetSubscribedChannels(DiscordFeedKind.DailyStep, msg.Mix, ct);
            if (channels.Count == 0) return;

            var charts = (await _mediator.Send(new GetChartsQuery(msg.Mix), ct)).ToDictionary(c => c.Id);
            var today = await _mediator.Send(new GetDailyStepQuery(msg.Mix), ct);
            var finished = msg.FinishedPlacements.OrderBy(p => p.Place).Take(TopPlayers).ToList();
            var names = await ResolveNames(finished.Select(p => p.UserId), ct);

            foreach (var channel in channels)
            {
                var labels = await CommunityLabelsFor(channel.ChannelId, ct);
                await _bot.SendRichMessages(
                    new[] { DailyCard(msg, finished, today, charts, names, labels, channel.Culture) },
                    new[] { channel.ChannelId }, ct);
            }
        }

        private RichBotMessage WeeklyResultCard(MixEnum mix, RankedBoard board,
            IReadOnlyDictionary<Guid, Chart> charts, IReadOnlyDictionary<Guid, string> names,
            IReadOnlyDictionary<Guid, string> labels,
            int cardIndex, int cardCount, int moreCharts, DateTimeOffset week, string? culture)
        {
            var chart = charts[board.ChartId];
            var rows = string.Join("\n", board.Placements.Select(p =>
                LeaderRow(mix, p.Item1, PlayerName(names, p.Item2.UserId), p.Item2.Score, p.Item2.Plate,
                    p.Item2.IsBroken, Label(labels, p.Item2.UserId))));
            var cardTag = _localizer.Get(culture, "Card {0} of {1}", cardIndex, cardCount);
            var footer = moreCharts > 0 && cardIndex == cardCount
                ? $"#MIX|{mix}# {cardTag} · {_localizer.Get(culture, "{0} more charts had entries", moreCharts)}"
                : $"#MIX|{mix}# {cardTag} · {mix.GetName()} · PIU Scores";

            // "m" is the culture's month-day pattern, so the week label reads naturally
            // in each language ("July 7", "7 juillet", "7月7日").
            var weekLabel = week.ToString("m", FormatCulture(culture));
            return new RichBotMessage(
                new RichBotSection(
                    $"### {_localizer.Get(culture, "Weekly Charts — final board")}\n-# {MixTag(mix)}{(string)chart.Song.Name} #DIFFICULTY|{chart.DifficultyString}# · {_localizer.Get(culture, "{0} players · week of {1}", board.PlayerCount, weekLabel)}",
                    chart.Song.ImagePath),
                new IRichBotBlock[] { new RichBotDivider(), new RichBotText(rows) },
                footer,
                mix.GetAccentColor(),
                Array.Empty<RichBotLink>());
        }

        private RichBotMessage LineupCard(MixEnum mix, IReadOnlyList<WeeklyTournamentChart> lineup,
            IReadOnlyDictionary<Guid, Chart> charts, IReadOnlyDictionary<Guid, Uri> videos, string? culture)
        {
            // Co-ops first; then by level low to high, singles before doubles at a tie.
            var resolved = lineup.Where(w => charts.ContainsKey(w.ChartId)).Select(w => charts[w.ChartId])
                .OrderBy(c => c.Type == ChartType.CoOp ? 0 : 1)
                .ThenBy(c => (int)c.Level)
                .ThenBy(c => c.Type == ChartType.Double ? 1 : 0)
                .ToList();
            var lines = string.Join("\n", resolved.Select(c =>
            {
                var video = videos.TryGetValue(c.Id, out var url)
                    ? $" - [{_localizer.Get(culture, "Video")}]({url})"
                    : string.Empty;
                return $"#DIFFICULTY|{c.DifficultyString}# [{(string)c.Song.Name}]({SiteBase}/Chart/{c.Id}){video}";
            }));

            return new RichBotMessage(
                new RichBotSection(
                    $"### {_localizer.Get(culture, "This week's charts")}\n-# {MixTag(mix)}{_localizer.Get(culture, "one per level bucket")}",
                    null),
                new IRichBotBlock[]
                {
                    new RichBotDivider(),
                    new RichBotText(string.IsNullOrEmpty(lines)
                        ? _localizer.Get(culture, "The new board is being drawn.")
                        : lines)
                },
                $"#MIX|{mix}# {mix.GetName()} · {_localizer.Get(culture, "resets Monday midnight ET")}",
                mix.GetAccentColor(),
                new[]
                {
                    new RichBotLink(_localizer.Get(culture, "Weekly Charts"),
                        new Uri($"{SiteBase}/WeeklyCharts"))
                });
        }

        private RichBotMessage DailyCard(DailyStepRotatedEvent msg, IReadOnlyList<DailyStepResult> finished,
            DailyStepBoard? today, IReadOnlyDictionary<Guid, Chart> charts,
            IReadOnlyDictionary<Guid, string> names, IReadOnlyDictionary<Guid, string> labels, string? culture)
        {
            var blocks = new List<IRichBotBlock> { new RichBotDivider() };

            if (finished.Count > 0 && charts.TryGetValue(msg.FinishedChartId, out var finishedChart))
            {
                blocks.Add(new RichBotText(
                    $"**{_localizer.Get(culture, "Yesterday")} — {(string)finishedChart.Song.Name} #DIFFICULTY|{finishedChart.DifficultyString}#**" +
                    (msg.FinishedIsLimbo ? " · 🕯 " + _localizer.Get(culture, "Limbo (lowest passing won)") : "")));
                blocks.Add(new RichBotText(string.Join("\n", finished.Select(p =>
                    LeaderRow(msg.Mix, p.Place, PlayerName(names, p.UserId), p.Score, p.Plate, p.IsBroken,
                        Label(labels, p.UserId))))));
                blocks.Add(new RichBotDivider());
            }

            if (today != null && charts.TryGetValue(today.ChartId, out var todayChart))
            {
                var banner = today.IsLimbo
                    ? "🕯 " + _localizer.Get(culture, "**Limbo Day** — lowest passing score wins. No breaking.")
                    : _localizer.Get(culture, "Highest score wins.");
                blocks.Add(new RichBotSection(
                    $"**{_localizer.Get(culture, "Today")} — [{(string)todayChart.Song.Name}]({SiteBase}/Chart/{todayChart.Id}) #DIFFICULTY|{todayChart.DifficultyString}#**\n-# {banner}",
                    todayChart.Song.ImagePath));
            }

            return new RichBotMessage(
                new RichBotSection(
                    $"### {_localizer.Get(culture, "Daily Step")}\n-# {MixTag(msg.Mix)}{_localizer.Get(culture, "yesterday's board settled")}",
                    null),
                blocks,
                $"#MIX|{msg.Mix}# {msg.Mix.GetName()} · {_localizer.Get(culture, "resets midnight ET")}",
                msg.Mix.GetAccentColor(),
                new[] { new RichBotLink(_localizer.Get(culture, "Daily Step board"), new Uri(SiteBase)) });
        }

        // The formatting culture for dates composed outside a localizer template.
        private static CultureInfo FormatCulture(string? culture) =>
            CultureInfo.GetCultureInfo(SupportedCultures.Normalize(culture));

        // The community name (when the row's player is in one of the channel's non-regional
        // communities) trails the row, e.g. "…SSS SG (Arrow Eclipse)".
        private static string LeaderRow(MixEnum mix, int place, string name, PhoenixScore score, PhoenixPlate plate,
            bool isBroken, string? community) =>
            $"`{place,2}` **{name}** — {(int)score:N0} #LETTERGRADE|{score.LetterGradeFor(mix)}|{isBroken}##PLATE|{plate}#"
            + (community != null ? $" ({community})" : string.Empty);

        private static string PlayerName(IReadOnlyDictionary<Guid, string> names, Guid userId) =>
            names.TryGetValue(userId, out var n) ? n : "Player";

        private static string? Label(IReadOnlyDictionary<Guid, string> labels, Guid userId) =>
            labels.TryGetValue(userId, out var label) ? label : null;

        private async Task<IReadOnlyDictionary<Guid, Uri>> VideoLinks(IEnumerable<Guid> chartIds, CancellationToken ct)
        {
            var ids = chartIds.Distinct().ToArray();
            if (ids.Length == 0) return new Dictionary<Guid, Uri>();
            var videos = await _mediator.Send(new GetChartVideosQuery(ids), ct);
            return videos.GroupBy(v => v.ChartId).ToDictionary(g => g.Key, g => g.First().VideoUrl);
        }

        // Phoenix is the default context and stays unprefixed; the tag marks the other mix.
        private static string MixTag(MixEnum mix) => mix == MixEnum.Phoenix ? "" : $"[{mix.GetName()}] ";

        private async Task<IReadOnlyDictionary<Guid, string>> ResolveNames(IEnumerable<Guid> userIds,
            CancellationToken ct)
        {
            var ids = userIds.Distinct().ToArray();
            if (ids.Length == 0) return new Dictionary<Guid, string>();
            var users = await _users.GetUsers(ids, ct);
            return users.ToDictionary(u => u.Id, u => (string)u.Name);
        }

        // Maps a member to the community name shown beside their row. Regional (country)
        // communities are excluded — everyone shares those, so they'd label every row.
        private async Task<IReadOnlyDictionary<Guid, string>> CommunityLabelsFor(ulong channelId, CancellationToken ct)
        {
            var communities = await _communities.GetChannelCommunities(channelId, ct);
            var labels = new Dictionary<Guid, string>();
            foreach (var community in communities.Where(c => !c.IsRegional))
            foreach (var userId in await _mediator.Send(new GetCommunityMembersQuery(community.Name), ct))
                labels.TryAdd(userId, (string)community.Name);
            return labels;
        }

        private sealed record RankedBoard(Guid ChartId, int PlayerCount,
            IReadOnlyList<(int, WeeklyTournamentEntry)> Placements);
    }
}
