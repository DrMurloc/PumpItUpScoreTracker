using MassTransit;
using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Events;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.OfficialMirror.Application
{
    /// <summary>
    ///     Posts the weekly official-leaderboard digest to its subscribed Discord channels
    ///     when a sweep seals. Lives in OfficialMirror (which owns the highlights and cutlines)
    ///     because Communities can't reference it — the vertical graph would cycle — so it
    ///     reads the channel subscriptions through the published <see cref="IDiscordFeedReader" />
    ///     and composes the card here.
    /// </summary>
    internal sealed class OfficialDigestFeedSaga : IConsumer<OfficialSnapshotSealedEvent>
    {
        private const string SiteBase = "https://piuscores.arroweclip.se";

        private readonly IBotClient _bot;
        private readonly IChartRepository _charts;
        private readonly IDiscordFeedReader _feeds;
        private readonly IMediator _mediator;

        public OfficialDigestFeedSaga(IBotClient bot, IChartRepository charts, IDiscordFeedReader feeds,
            IMediator mediator)
        {
            _bot = bot;
            _charts = charts;
            _feeds = feeds;
            _mediator = mediator;
        }

        public async Task Consume(ConsumeContext<OfficialSnapshotSealedEvent> context)
        {
            var msg = context.Message;
            if (msg.IsBaseline) return; // baseline seals only prime records — nothing to report
            var ct = context.CancellationToken;

            var channels = await _feeds.GetSubscribedChannels(DiscordFeedKinds.OfficialLeaderboards, msg.Mix, ct);
            if (channels.Count == 0) return;

            var highlights = await _mediator.Send(new GetWeeklyHighlightsQuery(msg.Mix), ct);
            if (highlights == null) return;
            var cutlines = await _mediator.Send(new GetWhatItTakesQuery(msg.Mix), ct);
            var rankings = await _mediator.Send(new GetOfficialRankingsQuery(msg.Mix), ct);
            var charts = (await _charts.GetCharts(msg.Mix, cancellationToken: ct)).ToDictionary(c => c.Id);

            var card = DigestCard(msg.Mix, highlights, cutlines, rankings, charts);
            if (card == null) return; // a quiet week (no movers, firsts, #1s, or full boards)
            await _bot.SendRichMessages(new[] { card }, channels, ct);
        }

        private static RichBotMessage? DigestCard(MixEnum mix, WeeklyHighlightsRecord highlights,
            WhatItTakesRecord? cutlines, OfficialRankingsRecord? rankings, IReadOnlyDictionary<Guid, Chart> charts)
        {
            var blocks = new List<IRichBotBlock>();

            // A separator fences each section so the card reads as grouped blocks rather than
            // one dense emoji wall.
            void AddSection(string heading, IEnumerable<string> lines)
            {
                if (blocks.Count > 0) blocks.Add(new RichBotDivider());
                blocks.Add(Section(heading, lines));
            }

            // Open with the current top 10 and each player's week-over-week rank move.
            if (rankings != null && rankings.Rankings.Count > 0)
                AddSection("🏆 **PUMBILITY top 10**", rankings.Rankings.Take(10).Select(r =>
                    $"`{r.Rank,2}` **{r.Player.Username}** — {r.Rating:N0} {RankMove(r.Rank, r.PreviousRank)}"));

            if (highlights.Movers.Count > 0)
                AddSection("📈 **PUMBILITY movers**", highlights.Movers.Take(5).Select(m =>
                    $"**{m.Player.Username}** #{m.PreviousRank} → **#{m.NewRank}** · {m.Pumbility:N2}"));

            if (highlights.BoardsClimbed.Count > 0)
                AddSection("🧗 **Boards climbed**", highlights.BoardsClimbed.Take(5).Select(b =>
                    $"**{b.Player.Username}** climbed {b.BoardsClimbed} boards (+{b.NetPlacesGained})"));

            if (highlights.WorldFirsts.Count > 0 || highlights.NewNumberOnes.Count > 0)
            {
                // Difficulties render as plain text here (not bubble emojis) so the highlight
                // lines don't stack emoji on emoji.
                var lines = highlights.WorldFirsts.Take(6).Select(f =>
                        $"First **{f.GradeBand}** — **{f.Player.Username}** {(f.IsFolderFirst ? $"in the {f.ChartType}{f.Level} folder" : $"on {ChartName(charts, f.ChartId)}")} · {f.Score:N0}")
                    .Concat(highlights.NewNumberOnes.Take(4).Select(n =>
                        $"New #1 — **{n.Player.Username}** on {ChartName(charts, n.ChartId)} · {n.Score:N0}" +
                        (n.Dethroned != null ? $", dethroning {n.Dethroned.Username}" : "")));
                AddSection("🌍 **World firsts & new #1s**", lines);
            }

            // What it takes, in difficulties: the uniform level where AAA/SSS on fifty charts
            // clears the rank-1000 cutline (null until the board is full at 1000).
            if (cutlines?.Entry != null)
            {
                var takes = new List<string>();
                if (cutlines.Entry.LevelForAAA != null)
                    takes.Add($"**50× AAA at Lv.{cutlines.Entry.LevelForAAA}**");
                if (cutlines.Entry.LevelForSSS != null)
                    takes.Add($"**50× SSS at Lv.{cutlines.Entry.LevelForSSS}**");
                if (takes.Count > 0)
                    AddSection("🎟 **To make the top 1000**", new[] { string.Join(" · ", takes) });
            }

            if (blocks.Count == 0) return null;

            var week = highlights.PreviousSnapshotAt != null
                ? $"vs {highlights.PreviousSnapshotAt:MMM d}"
                : "first week";
            var mixTag = mix == MixEnum.Phoenix ? "" : $"[{mix.GetName()}] ";
            return new RichBotMessage(
                new RichBotSection($"### This week on the official boards\n-# {mixTag}{week} · swept Sunday", null),
                blocks,
                $"#MIX|{mix}# {mix.GetName()} · PIU Scores official mirror",
                mix.GetAccentColor(),
                new[]
                {
                    new RichBotLink("This week", new Uri($"{SiteBase}/OfficialLeaderboards")),
                    new RichBotLink("What it takes", new Uri($"{SiteBase}/OfficialLeaderboards/WhatItTakes"))
                });
        }

        private static RichBotText Section(string heading, IEnumerable<string> lines) =>
            new($"{heading}\n{string.Join("\n", lines)}");

        private static string RankMove(int rank, int? previousRank)
        {
            if (previousRank == null) return "🆕";
            var delta = previousRank.Value - rank; // positive = moved up the board
            return delta > 0 ? $"↑{delta}" : delta < 0 ? $"↓{-delta}" : "–";
        }

        // Plain-text difficulty (e.g. "Paradoxx S26"), not a bubble emoji — the digest lines
        // already carry enough symbols.
        private static string ChartName(IReadOnlyDictionary<Guid, Chart> charts, Guid? chartId) =>
            chartId != null && charts.TryGetValue(chartId.Value, out var chart)
                ? $"{(string)chart.Song.Name} {chart.DifficultyString}"
                : "a chart";
    }
}
