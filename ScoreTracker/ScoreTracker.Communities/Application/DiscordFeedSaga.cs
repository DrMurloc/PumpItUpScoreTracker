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
        private readonly IMediator _mediator;
        private readonly IUserReader _users;

        public DiscordFeedSaga(IBotClient bot, IMediator mediator, IUserReader users,
            IDiscordFeedSubscriptionRepository feeds, ICommunityRepository communities)
        {
            _bot = bot;
            _mediator = mediator;
            _users = users;
            _feeds = feeds;
            _communities = communities;
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

            foreach (var channel in channels)
            {
                var members = await MembersFor(channel, ct);
                var cards = new List<RichBotMessage>();
                for (var i = 0; i < top.Count; i++)
                    cards.Add(WeeklyResultCard(mix, top[i], charts, names, members, i + 1, top.Count,
                        ranked.Count - top.Count, latest));
                cards.Add(LineupCard(mix, lineup, charts));
                await _bot.SendRichMessages(cards, new[] { channel }, ct);
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
                var members = await MembersFor(channel, ct);
                await _bot.SendRichMessages(
                    new[] { DailyCard(msg, finished, today, charts, names, members) }, new[] { channel }, ct);
            }
        }

        private RichBotMessage WeeklyResultCard(MixEnum mix, RankedBoard board,
            IReadOnlyDictionary<Guid, Chart> charts, IReadOnlyDictionary<Guid, string> names, ISet<Guid> members,
            int cardIndex, int cardCount, int moreCharts, DateTimeOffset week)
        {
            var chart = charts[board.ChartId];
            var rows = string.Join("\n", board.Placements.Select(p =>
                LeaderRow(p.Item1, Name(names, p.Item2.UserId, members), p.Item2.Score, p.Item2.Plate,
                    p.Item2.IsBroken)));
            var footer = moreCharts > 0 && cardIndex == cardCount
                ? $"#MIX|{mix}# Card {cardIndex} of {cardCount} · {moreCharts} more charts had entries"
                : $"#MIX|{mix}# Card {cardIndex} of {cardCount} · {mix.GetName()} · PIU Scores";

            return new RichBotMessage(
                new RichBotSection(
                    $"### Weekly Charts — final board\n-# {MixTag(mix)}{(string)chart.Song.Name} #DIFFICULTY|{chart.DifficultyString}# · {board.PlayerCount} players · week of {week:MMM d}",
                    chart.Song.ImagePath),
                new IRichBotBlock[] { new RichBotDivider(), new RichBotText(rows) },
                footer,
                mix.GetAccentColor(),
                Array.Empty<RichBotLink>());
        }

        private RichBotMessage LineupCard(MixEnum mix, IReadOnlyList<WeeklyTournamentChart> lineup,
            IReadOnlyDictionary<Guid, Chart> charts)
        {
            var resolved = lineup.Where(w => charts.ContainsKey(w.ChartId)).Select(w => charts[w.ChartId]).ToList();
            var lines = string.Join(" · ", resolved
                .OrderBy(c => c.Type).ThenBy(c => (int)c.Level)
                .Select(c => $"#DIFFICULTY|{c.DifficultyString}# [{(string)c.Song.Name}]({SiteBase}/Chart/{c.Id})"));

            return new RichBotMessage(
                new RichBotSection($"### This week's charts\n-# {MixTag(mix)}one per level bucket", null),
                new IRichBotBlock[]
                {
                    new RichBotDivider(),
                    new RichBotText(string.IsNullOrEmpty(lines) ? "The new board is being drawn." : lines)
                },
                $"#MIX|{mix}# {mix.GetName()} · resets Monday midnight ET",
                mix.GetAccentColor(),
                new[]
                {
                    new RichBotLink("Full results", new Uri($"{SiteBase}/WeeklyCharts")),
                    new RichBotLink("Weekly Charts", new Uri($"{SiteBase}/WeeklyCharts"))
                });
        }

        private RichBotMessage DailyCard(DailyStepRotatedEvent msg, IReadOnlyList<DailyStepResult> finished,
            DailyStepBoard? today, IReadOnlyDictionary<Guid, Chart> charts,
            IReadOnlyDictionary<Guid, string> names, ISet<Guid> members)
        {
            var blocks = new List<IRichBotBlock> { new RichBotDivider() };

            if (finished.Count > 0 && charts.TryGetValue(msg.FinishedChartId, out var finishedChart))
            {
                blocks.Add(new RichBotText(
                    $"**Yesterday — {(string)finishedChart.Song.Name} #DIFFICULTY|{finishedChart.DifficultyString}#**" +
                    (msg.FinishedIsLimbo ? " · 🕯 Limbo (lowest passing won)" : "")));
                blocks.Add(new RichBotText(string.Join("\n", finished.Select(p =>
                    LeaderRow(p.Place, Name(names, p.UserId, members), p.Score, p.Plate, p.IsBroken)))));
                blocks.Add(new RichBotDivider());
            }

            if (today != null && charts.TryGetValue(today.ChartId, out var todayChart))
            {
                var banner = today.IsLimbo
                    ? "🕯 **Limbo Day** — lowest passing score wins. No breaking."
                    : "Highest score wins.";
                blocks.Add(new RichBotSection(
                    $"**Today — [{(string)todayChart.Song.Name}]({SiteBase}/Chart/{todayChart.Id}) #DIFFICULTY|{todayChart.DifficultyString}#**\n-# {banner}",
                    todayChart.Song.ImagePath));
            }

            return new RichBotMessage(
                new RichBotSection($"### Daily Step\n-# {MixTag(msg.Mix)}yesterday's board settled", null),
                blocks,
                $"#MIX|{msg.Mix}# {msg.Mix.GetName()} · resets midnight ET",
                msg.Mix.GetAccentColor(),
                new[] { new RichBotLink("Daily Step board", new Uri(SiteBase)) });
        }

        private static string LeaderRow(int place, string name, PhoenixScore score, PhoenixPlate plate, bool isBroken) =>
            $"`{place,2}` **{name}** — {(int)score:N0} #LETTERGRADE|{score.LetterGrade}|{isBroken}##PLATE|{plate}#";

        private static string Name(IReadOnlyDictionary<Guid, string> names, Guid userId, ISet<Guid> members) =>
            (members.Contains(userId) ? "🟢 " : "") + (names.TryGetValue(userId, out var n) ? n : "Player");

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

        private async Task<ISet<Guid>> MembersFor(ulong channelId, CancellationToken ct)
        {
            var communityNames = await _communities.GetChannelCommunityNames(channelId, ct);
            if (communityNames.Count == 0) return new HashSet<Guid>();
            var members = new HashSet<Guid>();
            foreach (var name in communityNames)
                members.UnionWith(await _mediator.Send(new GetCommunityMembersQuery(name), ct));
            return members;
        }

        private sealed record RankedBoard(Guid ChartId, int PlayerCount,
            IReadOnlyList<(int, WeeklyTournamentEntry)> Placements);
    }
}
