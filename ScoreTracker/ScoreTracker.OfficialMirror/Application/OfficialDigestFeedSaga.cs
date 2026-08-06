using System.Globalization;
using MassTransit;
using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Events;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;
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

        /// <summary>
        ///     A normal week produces five or six firsts and they all belong on the card. The
        ///     cap is here for the week a song pack lands, when every new chart takes one.
        /// </summary>
        private const int MaxWorldFirsts = 6;

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
            var charts = (await _charts.GetCharts(msg.Mix, cancellationToken: ct)).ToDictionary(c => c.Id);

            // One composition per registered language, fanned out to that language's channels.
            foreach (var group in channels.GroupBy(c => c.Culture))
            {
                var card = DigestCard(msg.Mix, highlights, charts, group.Key);
                // Null means the snapshot carries nothing at all, not that the week was slow —
                // and that verdict is the same for every culture, so stopping here is stopping.
                if (card == null) return;
                await _bot.SendRichMessages(new[] { card }, group.Select(c => c.ChannelId).ToArray(), ct);
            }
        }

        private RichBotMessage? DigestCard(MixEnum mix, WeeklyHighlightsRecord highlights,
            IReadOnlyDictionary<Guid, Chart> charts, string? culture)
        {
            var blocks = new List<IRichBotBlock>();

            // The week's marquee chart: the highest-level world first, best score breaking a
            // tie — same pick the hub's hero makes. Its jacket is the card's only picture, and
            // the hype sentence is the only thing that says why it is there, so they live or
            // die together.
            var highestFirst = HighestFirst(highlights);
            var marqueeChart = highestFirst?.ChartId != null &&
                               charts.TryGetValue(highestFirst.ChartId.Value, out var found)
                ? found
                : null;

            // A separator fences each section so the card reads as grouped blocks rather than
            // one dense emoji wall. The lead is the block's first line — a heading on most of
            // them, the numbers themselves on the pulse.
            void AddSection(string lead, IEnumerable<string> lines)
            {
                if (blocks.Count > 0) blocks.Add(new RichBotDivider());
                blocks.Add(Section(lead, lines));
            }

            // The week in four numbers, before any list. Everything below is an example of
            // this; without it the card opened on a leaderboard and never said what happened.
            var pulse = highlights.Pulse;
            if (pulse != null)
                AddSection(
                    _localizer.Get(culture, "**{0}** board entries · **{1}** players active · **{2}** debuts",
                        Count(pulse.NewEntries + pulse.UpscoredEntries, culture),
                        Count(pulse.PlayersActive, culture), Count(pulse.DebutCount, culture)),
                    new[]
                    {
                        "-# " + _localizer.Get(culture, "{0} new · {1} upscored",
                            Count(pulse.NewEntries, culture), Count(pulse.UpscoredEntries, culture))
                    });

            // One name each, not five. The gainer leads on value won rather than places moved:
            // a rank jump off the crowded middle of the board can be enormous and mean little,
            // and the value is what the player actually earned.
            if (highlights.Gainers?.FirstOrDefault() is { } gainer)
                AddSection($"📈 **{_localizer.Get(culture, "Biggest PUMBILITY gain")}**", new[]
                {
                    _localizer.Get(culture, "{0} **+{1}** to {2} · #{3} → **#{4}**",
                        PlayerLink(gainer.Player),
                        (gainer.NewPumbility - gainer.PreviousPumbility).ToString("N2", FormatCulture(culture)),
                        gainer.NewPumbility.ToString("N2", FormatCulture(culture)),
                        Count(gainer.PreviousRank, culture), Count(gainer.NewRank, culture))
                });

            if (highlights.BoardsClimbed.FirstOrDefault() is { } climber)
            {
                var line = _localizer.Get(culture, "{0} **+{1} places** across {2} chart boards",
                    PlayerLink(climber.Player), Count(climber.NetPlacesGained, culture),
                    Count(climber.BoardsClimbed, culture));
                if (climber.NewBoards is { } fresh)
                    line += " · " + _localizer.Get(culture, "{0} new", Count(fresh, culture));
                AddSection($"🧗 **{_localizer.Get(culture, "Biggest board climber")}**", new[] { line });
            }

            // What holds each rung, in difficulties: the uniform level where fifty AAAs clear
            // the floor, this week against last. The hero draws the same two rungs at SS; AAA
            // is the yardstick this audience actually plays toward, and the card is read by
            // people who will never open the hub.
            //
            // FloorMark stores the SS level and nothing else, so the AAA equivalents are
            // computed here from the values the row does carry. GetWhatItTakesQuery cannot
            // stand in: its tier rows have no previous level at all, and its history covers
            // the #1000 floor alone, so it can never say what #100 took last week.
            var floors = highlights.Floors ?? Array.Empty<OfficialFloorMarkRecord>();
            if (floors.Count > 0)
            {
                var scoring = ScoringConfiguration.PumbilityScoring(mix, false);

                int? LevelFor(decimal value) => CutlineCalculator.LevelFor(scoring, ChartType.Single,
                    PhoenixLetterGrade.AAA, value);

                AddSection(
                    $"🎟 **{_localizer.Get(culture, "What holds the rungs")}** — " +
                    _localizer.Get(culture, "50× AAA on singles"),
                    floors.Select(f =>
                    {
                        var now = LevelFor(f.Value);
                        var was = f.PreviousValue == null ? null : LevelFor(f.PreviousValue.Value);
                        var level = now == null ? "—"
                            : was != null && was != now ? $"Lv.{was} → **Lv.{now}**"
                            : $"**Lv.{now}**";
                        var climb = f.PreviousValue is { } previous && f.Value > previous
                            ? $" ▲{(f.Value - previous).ToString("N0", FormatCulture(culture))}"
                            : "";
                        return $"`{"#" + f.Rank,5}` {level} · {f.Value.ToString("N2", FormatCulture(culture))}{climb}";
                    }));
            }

            // The firsts close the card. A busy week here is the payoff, not clutter, so the
            // block runs long on purpose — but the cap stays: a content drop hands every
            // brand-new chart a first at once, and forty rows would rebuild the wall of text
            // against Discord's own character ceiling.
            //
            // Ordered by level so the biggest leads, because that is the only emphasis this
            // format has. Components V2 text carries no background, border or size, so a
            // featured row can only be faked with a leading emoji, which reads as clutter.
            if (highlights.WorldFirsts.Count > 0)
            {
                // Each line: difficulty bubble, song, the words "World First", the grade as its
                // emoji (a PG rides its plate art), then the player linked to their board
                // profile. Folder firsts append their folder tag.
                var lines = FirstsByLevel(highlights).Take(MaxWorldFirsts).Select(f =>
                {
                    var chart = f.ChartId != null && charts.TryGetValue(f.ChartId.Value, out var c) ? c : null;
                    var bubble = chart == null ? "" : $"#DIFFICULTY|{chart.DifficultyString}# ";
                    var song = chart == null
                        ? _localizer.Get(culture, "a chart")
                        : (string)chart.Song.Name;
                    var grade = f.GradeBand == "PG"
                        ? $"#PLATE|{PhoenixPlate.PerfectGame}#"
                        : $"#LETTERGRADE|{PhoenixLetterGradeHelperMethods.TryParse(f.GradeBand)}#";
                    var folder = f.IsFolderFirst && f.ChartType != null && f.Level != null
                        ? " · " + _localizer.Get(culture, "{0} folder first",
                            $"{(f.ChartType == ChartType.Double.ToString() ? "D" : "S")}{f.Level}")
                        : "";
                    return $"{bubble}**{song}** — {_localizer.Get(culture, "World First")} {grade} — " +
                           $"{PlayerLink(f.Player)}{folder}";
                });
                AddSection($"🌍 **{_localizer.Get(culture, "World firsts")}**", lines);
            }

            // Only a snapshot carrying no pulse row and no highlights at all reaches this —
            // never a slow week, which still has entries and players to report.
            if (blocks.Count == 0) return null;

            // "m" is the culture's month-day pattern, so the week tag reads naturally.
            var week = highlights.PreviousSnapshotAt != null
                ? _localizer.Get(culture, "vs {0}",
                    highlights.PreviousSnapshotAt.Value.ToString("m", FormatCulture(culture)))
                : _localizer.Get(culture, "first week");
            var mixTag = mix == MixEnum.Phoenix ? "" : $"[{mix.GetName()}] ";
            var hype = Hype(pulse, highestFirst, marqueeChart, culture);
            return new RichBotMessage(
                new RichBotSection(
                    $"### {_localizer.Get(culture, "This week on the official boards")}\n-# {mixTag}{week}{hype}",
                    marqueeChart?.Song.ImagePath),
                blocks,
                $"#MIX|{mix}# {mix.GetName()} · {_localizer.Get(culture, "PIU Scores official mirror")}",
                mix.GetAccentColor(),
                new[]
                {
                    new RichBotLink(_localizer.Get(culture, "This Week"),
                        new Uri($"{SiteBase}/OfficialLeaderboards")),
                    new RichBotLink(_localizer.Get(culture, "What It Takes"),
                        new Uri($"{SiteBase}/OfficialLeaderboards/WhatItTakes"))
                });
        }

        private static RichBotText Section(string heading, IEnumerable<string> lines) =>
            new($"{heading}\n{string.Join("\n", lines)}");

        // Board tags are TAG#1234 and the card prints the human half, same as every list on
        // the hub — the digits identify an account, they don't name anyone. The link keeps the
        // whole tag in its query parameter, which is what /Players resolves on.
        private static string PlayerLink(OfficialPlayerRecord player) =>
            $"[{OfficialPlayerNames.Human(player.Username)}]({SiteBase}/OfficialLeaderboards/Players" +
            $"?player={Uri.EscapeDataString(player.Username)})";

        // Counts are grouped in the reader's locale before they reach a template, because the
        // templates carry no format specifier of their own.
        private static string Count(int value, string? culture) =>
            value.ToString("N0", FormatCulture(culture));

        /// <summary>Firsts ranked by how hard they were: level first, the better score breaking ties.</summary>
        private static IEnumerable<OfficialGradeFirstRecord> FirstsByLevel(WeeklyHighlightsRecord highlights) =>
            highlights.WorldFirsts
                .OrderByDescending(f => f.Level ?? 0)
                .ThenByDescending(f => f.Score);

        /// <summary>
        ///     The marquee first, whose jacket the card wears. Null when the week produced none,
        ///     which is how the card loses its picture rather than showing an unrelated one.
        /// </summary>
        private static OfficialGradeFirstRecord? HighestFirst(WeeklyHighlightsRecord highlights) =>
            FirstsByLevel(highlights).FirstOrDefault();

        // The hub's own hype line, and the caption for the jacket above it. A week with no
        // first still gets the players-only half; a week with no pulse row gets nothing, since
        // the sentence is built around the count.
        private string Hype(WeeklyPulseRecord? pulse, OfficialGradeFirstRecord? highest, Chart? chart,
            string? culture)
        {
            if (pulse == null) return "";
            var players = Count(pulse.PlayersActive, culture);
            return " · " + (chart == null || highest == null
                ? _localizer.Get(culture, "{0} players left their mark on the chart boards.", players)
                : _localizer.Get(culture, "{0} players left their mark — and {1} fell to its first {2}.",
                    players, $"{(string)chart.Song.Name} {chart.DifficultyString}", highest.GradeBand));
        }

        // The formatting culture for dates composed outside a localizer template.
        private static CultureInfo FormatCulture(string? culture) =>
            CultureInfo.GetCultureInfo(SupportedCultures.Normalize(culture));
    }
}
