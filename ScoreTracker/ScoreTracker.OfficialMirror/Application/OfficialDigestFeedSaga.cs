using System.Globalization;
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
    ///     and composes the card here, once per registered language.
    /// </summary>
    internal sealed class OfficialDigestFeedSaga : IConsumer<OfficialSnapshotSealedEvent>
    {
        private const string SiteBase = "https://piuscores.arroweclip.se";

        private readonly IBotClient _bot;
        private readonly IChartRepository _charts;
        private readonly IDiscordFeedReader _feeds;
        private readonly ILocalizedTextAccessor _localizer;
        private readonly IMediator _mediator;

        public OfficialDigestFeedSaga(IBotClient bot, IChartRepository charts, IDiscordFeedReader feeds,
            IMediator mediator, ILocalizedTextAccessor localizer)
        {
            _bot = bot;
            _charts = charts;
            _feeds = feeds;
            _mediator = mediator;
            _localizer = localizer;
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

            // One composition per registered language, fanned out to that language's channels.
            foreach (var group in channels.GroupBy(c => c.Culture))
            {
                var card = DigestCard(msg.Mix, highlights, cutlines, rankings, charts, group.Key);
                if (card == null) return; // a quiet week (no movers, firsts, #1s, or full boards)
                await _bot.SendRichMessages(new[] { card }, group.Select(c => c.ChannelId).ToArray(), ct);
            }
        }

        private RichBotMessage? DigestCard(MixEnum mix, WeeklyHighlightsRecord highlights,
            WhatItTakesRecord? cutlines, OfficialRankingsRecord? rankings, IReadOnlyDictionary<Guid, Chart> charts,
            string? culture)
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
                AddSection($"🏆 **{_localizer.Get(culture, "PUMBILITY top 10")}**", rankings.Rankings.Take(10)
                    .Select(r => $"`{r.Rank,2}` **{r.Player.Username}** — {r.Rating:N0} {RankMove(r.Rank, r.PreviousRank)}"));

            if (highlights.Movers.Count > 0)
                AddSection($"📈 **{_localizer.Get(culture, "PUMBILITY movers")}**", highlights.Movers.Take(5).Select(m =>
                    $"**{m.Player.Username}** #{m.PreviousRank} → **#{m.NewRank}** · {m.Pumbility:N2}"));

            if (highlights.BoardsClimbed.Count > 0)
                AddSection($"🧗 **{_localizer.Get(culture, "Boards climbed")}**", highlights.BoardsClimbed.Take(5)
                    .Select(b => _localizer.Get(culture, "**{0}** climbed {1} boards (+{2})",
                        b.Player.Username, b.BoardsClimbed, b.NetPlacesGained)));

            if (highlights.WorldFirsts.Count > 0 || highlights.NewNumberOnes.Count > 0)
            {
                // Difficulties render as plain text here (not bubble emojis) so the highlight
                // lines don't stack emoji on emoji.
                var lines = highlights.WorldFirsts.Take(6).Select(f => f.IsFolderFirst
                        ? _localizer.Get(culture, "First **{0}** — **{1}** in the {2} folder · {3:N0}",
                            f.GradeBand, f.Player.Username, $"{f.ChartType}{f.Level}", f.Score)
                        : _localizer.Get(culture, "First **{0}** — **{1}** on {2} · {3:N0}",
                            f.GradeBand, f.Player.Username, ChartName(charts, f.ChartId, culture), f.Score))
                    .Concat(highlights.NewNumberOnes.Take(4).Select(n =>
                        _localizer.Get(culture, "New #1 — **{0}** on {1} · {2:N0}",
                            n.Player.Username, ChartName(charts, n.ChartId, culture), n.Score) +
                        (n.Dethroned != null
                            ? _localizer.Get(culture, ", dethroning {0}", n.Dethroned.Username)
                            : "")));
                AddSection($"🌍 **{_localizer.Get(culture, "World firsts & new #1s")}**", lines);
            }

            // What it takes, in difficulties: the uniform level where AAA/SSS on fifty charts
            // clears the rank-1000 cutline (null until the board is full at 1000).
            if (cutlines?.Entry != null)
            {
                var takes = new List<string>();
                if (cutlines.Entry.LevelForAAA != null)
                    takes.Add(_localizer.Get(culture, "**50× AAA at Lv.{0}**", cutlines.Entry.LevelForAAA));
                if (cutlines.Entry.LevelForSSS != null)
                    takes.Add(_localizer.Get(culture, "**50× SSS at Lv.{0}**", cutlines.Entry.LevelForSSS));
                if (takes.Count > 0)
                    AddSection($"🎟 **{_localizer.Get(culture, "To make the top 1000")}**",
                        new[] { string.Join(" · ", takes) });
            }

            if (blocks.Count == 0) return null;

            // "m" is the culture's month-day pattern, so the week tag reads naturally.
            var week = highlights.PreviousSnapshotAt != null
                ? _localizer.Get(culture, "vs {0}",
                    highlights.PreviousSnapshotAt.Value.ToString("m", FormatCulture(culture)))
                : _localizer.Get(culture, "first week");
            var mixTag = mix == MixEnum.Phoenix ? "" : $"[{mix.GetName()}] ";
            return new RichBotMessage(
                new RichBotSection(
                    $"### {_localizer.Get(culture, "This week on the official boards")}\n-# {mixTag}{week} · {_localizer.Get(culture, "swept Sunday")}",
                    null),
                blocks,
                $"#MIX|{mix}# {mix.GetName()} · {_localizer.Get(culture, "PIU Scores official mirror")}",
                mix.GetAccentColor(),
                new[]
                {
                    new RichBotLink(_localizer.Get(culture, "This week"),
                        new Uri($"{SiteBase}/OfficialLeaderboards")),
                    new RichBotLink(_localizer.Get(culture, "What it takes"),
                        new Uri($"{SiteBase}/OfficialLeaderboards/WhatItTakes"))
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
        private string ChartName(IReadOnlyDictionary<Guid, Chart> charts, Guid? chartId, string? culture) =>
            chartId != null && charts.TryGetValue(chartId.Value, out var chart)
                ? $"{(string)chart.Song.Name} {chart.DifficultyString}"
                : _localizer.Get(culture, "a chart");

        // The formatting culture for dates composed outside a localizer template.
        private static CultureInfo FormatCulture(string? culture) =>
            CultureInfo.GetCultureInfo(SupportedCultures.Normalize(culture));
    }
}
