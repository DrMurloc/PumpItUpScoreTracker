using MassTransit;
using MediatR;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Identity.Contracts.Events;
using ScoreTracker.Ucs.Contracts.Events;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.PlayerProgress.Contracts.Queries;

namespace ScoreTracker.Communities.Application;

internal sealed class CommunitySaga : IRequestHandler<CreateCommunityCommand>, IRequestHandler<JoinCommunityCommand>,
    IRequestHandler<LeaveCommunityCommand>,
    IRequestHandler<GetCommunityMembersQuery, IEnumerable<Guid>>,
    IRequestHandler<GetCommunityLeaderboardQuery, IEnumerable<CommunityLeaderboardRecord>>,
    IRequestHandler<CreateInviteLinkCommand, Guid>,
    IRequestHandler<GetMyCommunitiesQuery, IEnumerable<CommunityOverviewRecord>>,
    IRequestHandler<GetPublicCommunitiesQuery, IEnumerable<CommunityOverviewRecord>>,
    IRequestHandler<GetCommunityQuery, Community>,
    IRequestHandler<JoinCommunityByInviteCodeCommand>,
    IRequestHandler<AddDiscordChannelToCommunityCommand>,
    IRequestHandler<RemoveDiscordChannelFromCommunityCommand>,
    IRequestHandler<GetPhoenixRecordsForCommunityQuery, IEnumerable<UserPhoenixScore>>,
    IConsumer<PlayerRatingsImprovedEvent>,
    IConsumer<ScoreHighlightsCapturedEvent>,
    IConsumer<NewTitlesAcquiredEvent>,
    IConsumer<UserWeeklyChartsProgressedEvent>,
    IConsumer<UserUpdatedEvent>,
    IConsumer<UcsLeaderboardPlacedEvent>

{
    private readonly IBotClient _bot;
    private readonly IChartRepository _charts;
    private readonly ICommunityRepository _communities;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;
    private readonly IScoreReader _scores;
    private readonly IUserReader _users;
    private readonly IDateTimeOffsetAccessor _dateTime;

    public CommunitySaga(ICurrentUserAccessor currentUser, ICommunityRepository communities, IBotClient bot,
        IUserReader users, IChartRepository charts, IScoreReader scores, IMediator mediator,
        IDateTimeOffsetAccessor dateTime)
    {
        _currentUser = currentUser;
        _communities = communities;
        _bot = bot;
        _users = users;
        _charts = charts;
        _scores = scores;
        _mediator = mediator;
        _dateTime = dateTime;
    }

    public async Task Consume(ConsumeContext<NewTitlesAcquiredEvent> context)
    {
        var prefix = MixPrefix(context.Message.Mix);
        var user = await _users.GetUser(context.Message.UserId, context.CancellationToken);
        var message = string.Empty;
        var count = 0;
        if (context.Message.NewTitles.Any())
        {
            message = $"**{user.Name}** completed the Titles:";
            foreach (var title in context.Message.NewTitles.OrderBy(t => t))
            {
                message += $@"
- {title}";

                count++;
                if (count != 10) continue;

                await SendToCommunityDiscords(user.Id, prefix + message,
                    context.CancellationToken);
                message = "";
                count = 0;
            }
        }

        if (context.Message.ParagonUpgrades.Any())
        {
            if (!string.IsNullOrWhiteSpace(message))
                message += @"
";
            message += $"**{user.Name}** Advanced their Paragon Title Levels:";
            foreach (var upgradedTitle in context.Message.ParagonUpgrades.OrderBy(t => t.Key))
            {
                var emoji = upgradedTitle.Value == "PG"
                    ? "#PLATE|PerfectGame#"
                    : "#LETTERGRADE|" + upgradedTitle.Value + "#";
                message += $@"
- {upgradedTitle.Key} {emoji}";
                count++;
                if (count != 10) continue;

                await SendToCommunityDiscords(user.Id,
                    prefix + message, context.CancellationToken);
                message = "";
                count = 0;
            }
        }

        if (!string.IsNullOrWhiteSpace(message))
            await SendToCommunityDiscords(user.Id,
                prefix + message, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<PlayerRatingsImprovedEvent> context)
    {
        var user = await _users.GetUser(context.Message.UserId, context.CancellationToken);
        if (user == null) return;

        var message = string.Empty;
        if (context.Message.NewTop50 > context.Message.OldTop50)
            message += $@"
- PUMBILITY improved to {context.Message.NewTop50} (+{context.Message.NewTop50 - context.Message.OldTop50})";
        if (context.Message.NewSinglesTop50 > context.Message.OldSinglesTop50)
            message += $@"
- PUMBILITY Singles to {context.Message.NewSinglesTop50} (+{context.Message.NewSinglesTop50 - context.Message.OldSinglesTop50})";
        if (context.Message.NewDoublesTop50 > context.Message.OldDoublesTop50)
            message += $@"
- PUMBILITY Doubles improved to {context.Message.NewDoublesTop50} (+{context.Message.NewDoublesTop50 - context.Message.OldDoublesTop50})";


        if (context.Message.NewCompetitive > context.Message.OldCompetitive &&
            context.Message.NewCompetitive.ToString("0.000") !=
            context.Message.OldCompetitive.ToString("0.000"))
            message += $@"
- Competitive Level improved to {context.Message.NewCompetitive:0.00000} (+{context.Message.NewCompetitive - context.Message.OldCompetitive:0.000})";
        if (context.Message.NewSinglesCompetitive > context.Message.OldSinglesCompetitive &&
            context.Message.NewSinglesCompetitive.ToString("0.000") !=
            context.Message.OldSinglesCompetitive.ToString("0.000"))
            message += $@"
- Singles Competitive Level improved to {context.Message.NewSinglesCompetitive:0.000} (+{context.Message.NewSinglesCompetitive - context.Message.OldSinglesCompetitive:0.000})";
        if (context.Message.NewDoublesCompetitive > context.Message.OldDoublesCompetitive &&
            context.Message.NewDoublesCompetitive.ToString("0.000") !=
            context.Message.OldDoublesCompetitive.ToString("0.000"))
            message += $@"
- Doubles Competitive Level improved to {context.Message.NewDoublesCompetitive:0.000} (+{context.Message.NewDoublesCompetitive - context.Message.OldDoublesCompetitive:0.000})";
        if (!string.IsNullOrWhiteSpace(message))
            await SendToCommunityDiscords(context.Message.UserId,
                MixPrefix(context.Message.Mix) + $"**{user.Name}**'s top 50 rating has improved!" + message,
                context.CancellationToken);
    }

    // The score cards render from ScoreHighlightsCapturedEvent — published by highlight
    // capture AFTER the flags persist — so the badges are deterministic instead of
    // racing capture. Card and chart lookups follow the event's mix so parallel-mix
    // announcements read the right ledger slice.
    private const int DigestThreshold = 25;
    private const int ArtRowCap = 5;
    private const int SmallSessionArtMax = 3;
    private const int UpscoreRowsPerCard = 12;
    private const string SiteBase = "https://piuscores.arroweclip.se";

    public async Task Consume(ConsumeContext<ScoreHighlightsCapturedEvent> context)
    {
        var e = context.Message;
        var user = await _users.GetUser(e.UserId, context.CancellationToken);
        if (user == null) return;

        var bests = (await _scores.GetBestScores(e.Mix, e.UserId, context.CancellationToken))
            .Where(s => s.Score != null)
            .ToDictionary(s => s.ChartId);
        var charts = (await _charts.GetCharts(e.Mix,
                chartIds: e.Changes.Select(c => c.ChartId).Distinct(),
                cancellationToken: context.CancellationToken))
            .ToDictionary(c => c.Id);
        var scoringLevels = await _mediator.Send(new GetChartScoringLevelsQuery(e.Mix), context.CancellationToken);

        // Flagged rows lead (they own the art slots), then the universal noteworthy
        // ordering: difficulty level desc, then scoring level desc (community decimal
        // difficulty), then score — the same composite the Sessions page uses.
        var known = e.Changes
            .Where(c => charts.ContainsKey(c.ChartId) && bests.ContainsKey(c.ChartId))
            .OrderByDescending(c => c.Flags != HighlightFlag.None)
            .ThenByDescending(c => (int)charts[c.ChartId].Level)
            .ThenByDescending(c => scoringLevels.TryGetValue(c.ChartId, out var sl) ? sl : 0)
            .ThenByDescending(c => (int)(bests[c.ChartId].Score ?? 0))
            .ToArray();
        if (!known.Any()) return;
        var passes = known.Where(c => c.IsNewPass).ToArray();
        var upscores = known.Where(c => !c.IsNewPass).ToArray();
        var banner = MilestoneBanner(e.Milestones);

        var header = $"### {MixPrefix(e.Mix)}**{user.Name}** ";
        var footer = $"#MIX|{e.Mix}# {e.Mix.GetName()} · PIU Scores";
        var accent = AccentFor(known.Select(c => bests[c.ChartId]));
        // The deep link only renders for public players — the Sessions page redirects
        // everyone else home anyway.
        var links = user.IsPublic
            ? new[]
            {
                new RichBotLink("View all recent scores",
                    new Uri($"{SiteBase}/Player/{user.Id}/Sessions" +
                            (e.SessionId == null ? string.Empty : $"?session={e.SessionId}")))
            }
            : Array.Empty<RichBotLink>();

        var messages = new List<RichBotMessage>();
        if (known.Length > DigestThreshold)
        {
            messages.Add(await BuildDigestCard(e, user, known, passes, charts, bests, header, footer, accent,
                links, banner, context.CancellationToken));
        }
        else
        {
            if (passes.Any())
                messages.Add(await BuildPassesCard(e, user, passes, charts, bests, header, footer, accent, links,
                    banner, context.CancellationToken));
            if (upscores.Any())
                messages.AddRange(BuildUpscoreCards(user, upscores, charts, bests, header, footer, accent, links,
                    withArt: known.Length <= SmallSessionArtMax,
                    banner: passes.Any() ? null : banner));
        }

        await SendRichToCommunityDiscords(user.Id, messages, context.CancellationToken);
    }

    private async Task<RichBotMessage> BuildPassesCard(ScoreHighlightsCapturedEvent e, User user,
        ScoreHighlightsCapturedEvent.HighlightedChange[] passes, IDictionary<Guid, Chart> charts,
        IDictionary<Guid, RecordedPhoenixScore> bests, string header, string footer, uint accent,
        RichBotLink[] links, string? banner, CancellationToken cancellationToken)
    {
        var blocks = OpenWithBanner(banner);

        // Flagged passes always render as individual rows (art while slots last, plain
        // text after) so no highlight ever collapses into the grouped overflow; a
        // divider fences them off from the unflagged remainder.
        var flagged = passes.Where(p => p.Flags != HighlightFlag.None).ToArray();
        var unflagged = passes.Where(p => p.Flags == HighlightFlag.None).ToArray();
        foreach (var pass in flagged.Take(ArtRowCap))
            blocks.Add(new RichBotSection(PassRow(pass, charts[pass.ChartId], bests[pass.ChartId]),
                charts[pass.ChartId].Song.ImagePath));
        foreach (var pass in flagged.Skip(ArtRowCap))
            blocks.Add(new RichBotText(PassRow(pass, charts[pass.ChartId], bests[pass.ChartId])));
        if (flagged.Any() && unflagged.Any()) blocks.Add(new RichBotDivider());

        var artLeft = ArtRowCap - Math.Min(flagged.Length, ArtRowCap);
        foreach (var pass in unflagged.Take(artLeft))
            blocks.Add(new RichBotSection(PassRow(pass, charts[pass.ChartId], bests[pass.ChartId]),
                charts[pass.ChartId].Song.ImagePath));

        var rest = unflagged.Skip(artLeft).ToArray();
        if (rest.Any())
        {
            var grouped = rest
                .GroupBy(c => charts[c.ChartId].DifficultyString)
                .OrderByDescending(g => (int)charts[g.First().ChartId].Level)
                .Select(g => $"{g.Key} ×{g.Count()}");
            blocks.Add(new RichBotText($"+{rest.Length} more: {string.Join(", ", grouped)}"));
        }

        var stats = await FolderProgress(e.Mix, e.UserId, passes.Select(c => charts[c.ChartId]), null,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(stats))
        {
            blocks.Add(new RichBotDivider());
            blocks.Add(new RichBotText(stats));
        }

        return new RichBotMessage(
            new RichBotSection($"{header}passed {passes.Length:N0} {Charts(passes.Length)}", user.ProfileImage),
            blocks, footer, accent, links);
    }

    private IEnumerable<RichBotMessage> BuildUpscoreCards(User user,
        ScoreHighlightsCapturedEvent.HighlightedChange[] upscores, IDictionary<Guid, Chart> charts,
        IDictionary<Guid, RecordedPhoenixScore> bests, string header, string footer, uint accent,
        RichBotLink[] links, bool withArt, string? banner)
    {
        var headerSection = new RichBotSection($"{header}upscored {upscores.Length:N0} {Charts(upscores.Length)}",
            user.ProfileImage);
        if (withArt)
        {
            // A small session's upscores earn the same ceremony as passes.
            var blocks = OpenWithBanner(banner);
            blocks.AddRange(upscores.Select(u => (IRichBotBlock)new RichBotSection(
                UpscoreRow(u, charts[u.ChartId], bests[u.ChartId]), charts[u.ChartId].Song.ImagePath)));
            yield return new RichBotMessage(headerSection, blocks, footer, accent, links);
            yield break;
        }

        for (var offset = 0; offset < upscores.Length; offset += UpscoreRowsPerCard)
        {
            var rows = upscores.Skip(offset).Take(UpscoreRowsPerCard)
                .Select(u => UpscoreRow(u, charts[u.ChartId], bests[u.ChartId]));
            var blocks = OpenWithBanner(offset == 0 ? banner : null);
            blocks.Add(new RichBotText(string.Join("\n", rows)));
            yield return new RichBotMessage(headerSection, blocks, footer, accent, links);
        }
    }

    private async Task<RichBotMessage> BuildDigestCard(ScoreHighlightsCapturedEvent e, User user,
        ScoreHighlightsCapturedEvent.HighlightedChange[] known,
        ScoreHighlightsCapturedEvent.HighlightedChange[] passes, IDictionary<Guid, Chart> charts,
        IDictionary<Guid, RecordedPhoenixScore> bests, string header, string footer, uint accent,
        RichBotLink[] links, string? banner, CancellationToken cancellationToken)
    {
        // One calm card per import, no matter the dump size: the milestone banner,
        // highlights (flagged scores lead — `known` arrives flagged-first), the level
        // span, and the top folders. The button carries the full enumeration.
        var blocks = OpenWithBanner(banner);
        var flaggedCount = known.Count(c => c.Flags != HighlightFlag.None);
        var highlights = known.Take(ArtRowCap).ToArray();
        foreach (var highlight in highlights)
            blocks.Add(new RichBotSection(highlight.IsNewPass
                    ? PassRow(highlight, charts[highlight.ChartId], bests[highlight.ChartId])
                    : UpscoreRow(highlight, charts[highlight.ChartId], bests[highlight.ChartId]),
                charts[highlight.ChartId].Song.ImagePath));
        if (flaggedCount > highlights.Length)
            blocks.Add(new RichBotText($"…and {flaggedCount - highlights.Length:N0} more highlights"));

        blocks.Add(new RichBotDivider());
        var span = LevelSpan(known.Select(c => charts[c.ChartId]).ToArray());
        var highestPass = passes.Select(c => charts[c.ChartId]).FirstOrDefault();
        if (span.Length > 0)
            blocks.Add(new RichBotText(highestPass == null
                ? $"Levels {span}"
                : $"Levels {span} — highest new pass {highestPass.DifficultyString}"));

        var stats = await FolderProgress(e.Mix, e.UserId, passes.Select(c => charts[c.ChartId]), 3,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(stats)) blocks.Add(new RichBotText(stats));

        var title = passes.Any() && known.Length > passes.Length
            ? $"{header}passed {passes.Length:N0} · upscored {known.Length - passes.Length:N0}"
            : passes.Any()
                ? $"{header}passed {passes.Length:N0} {Charts(passes.Length)}"
                : $"{header}upscored {known.Length:N0} {Charts(known.Length)}";
        return new RichBotMessage(new RichBotSection(title, user.ProfileImage), blocks, footer, accent, links);
    }

    private async Task<string> FolderProgress(MixEnum mix, Guid userId, IEnumerable<Chart> passCharts,
        int? topFolders, CancellationToken cancellationToken)
    {
        var groups = passCharts.GroupBy(c => (c.Type, c.Level))
            .OrderByDescending(g => g.Count()).ThenByDescending(g => (int)g.Key.Level)
            .ToArray();
        if (topFolders != null) groups = groups.Take(topFolders.Value).ToArray();
        var parts = new List<string>();
        foreach (var group in groups.OrderByDescending(g => (int)g.Key.Level).ThenBy(g => g.Key.Type))
        {
            var total = (await _mediator.Send(new GetChartsQuery(mix, group.Key.Level, group.Key.Type),
                cancellationToken)).Count();
            var clears = await _scores.GetClearCount(mix, userId, group.Key.Type, group.Key.Level,
                cancellationToken);
            if (total > 0)
                parts.Add(
                    $"#DIFFICULTY|{group.Key.Type.GetShortHand()}{group.Key.Level}# {clears}/{total} ({100.0 * clears / total:0.0}%)");
        }

        return string.Join(" · ", parts);
    }

    private static string LevelSpan(IReadOnlyList<Chart> charts)
    {
        var parts = new List<string>();
        foreach (var (type, shortHand) in new[] { (ChartType.Single, "S"), (ChartType.Double, "D") })
        {
            var levels = charts.Where(c => c.Type == type).Select(c => (int)c.Level).ToArray();
            if (!levels.Any()) continue;
            var min = levels.Min();
            var max = levels.Max();
            parts.Add(min == max ? $"{shortHand}{min}" : $"{shortHand}{min}–{shortHand}{max}");
        }

        return string.Join(" · ", parts);
    }

    private static string PassRow(ScoreHighlightsCapturedEvent.HighlightedChange change, Chart chart,
        RecordedPhoenixScore best)
    {
        return $"#DIFFICULTY|{chart.DifficultyString}# {SongLink(change, chart)}\n" +
               $"**{(int)best.Score!.Value:N0}** #LETTERGRADE|{best.Score!.Value.LetterGrade}|{best.IsBroken}##PLATE|{best.Plate}#" +
               FlagCaption(change.Flags);
    }

    private static string UpscoreRow(ScoreHighlightsCapturedEvent.HighlightedChange change, Chart chart,
        RecordedPhoenixScore best)
    {
        var row = $"#DIFFICULTY|{chart.DifficultyString}# {SongLink(change, chart)} **{(int)best.Score!.Value:N0}**";
        if (change.OldScore != null)
        {
            row += $" (+{(int)best.Score!.Value - change.OldScore.Value:N0})";
            var oldLetter = PhoenixScore.From(change.OldScore.Value).LetterGrade;
            if (oldLetter != best.Score!.Value.LetterGrade)
                row += $" #LETTERGRADE|{oldLetter}|False# →";
        }

        return row + $" #LETTERGRADE|{best.Score!.Value.LetterGrade}|{best.IsBroken}##PLATE|{best.Plate}#" +
               FlagCaption(change.Flags);
    }

    private static string SongLink(ScoreHighlightsCapturedEvent.HighlightedChange change, Chart chart)
    {
        var link = $"[{chart.Song.Name}]({SiteBase}/Chart/{chart.Id})";
        return change.Flags == HighlightFlag.None ? link : $"**{link}**";
    }

    // The why-it's-noteworthy caption, rendered as Discord subtext under the score.
    // Vocabulary mirrors the Sessions page badge tooltips.
    private static string FlagCaption(HighlightFlag flags)
    {
        if (flags == HighlightFlag.None) return string.Empty;
        var parts = new List<string>();
        if (flags.HasFlag(HighlightFlag.PumbilityTop50)) parts.Add("👑 PUMBILITY top 50");
        if (flags.HasFlag(HighlightFlag.ScoreQuality90)) parts.Add("📊 Top scores among peers");
        if (flags.HasFlag(HighlightFlag.TitleProgress)) parts.Add("🏅 Title progress");
        if (flags.HasFlag(HighlightFlag.FolderDebut)) parts.Add("🆕 Folder debut");
        if (flags.HasFlag(HighlightFlag.FolderCompletion90)) parts.Add("📁 Nearly complete folder");
        if (flags.HasFlag(HighlightFlag.CompetitiveImprover)) parts.Add("⬆ Raised competitive level");
        return "\n-# " + string.Join(" · ", parts);
    }

    // Milestones open the card as their own band between two separators — the loudest
    // thing on it short of the header. Capture-side milestones only (folder lamps);
    // rating and title milestones ride their own announcement paths.
    private const int BannerLineCap = 6;

    private static List<IRichBotBlock> OpenWithBanner(string? banner)
    {
        var blocks = new List<IRichBotBlock> { new RichBotDivider() };
        if (banner == null) return blocks;
        blocks.Add(new RichBotText(banner));
        blocks.Add(new RichBotDivider());
        return blocks;
    }

    private static string? MilestoneBanner(IReadOnlyList<PlayerMilestoneRecord> milestones)
    {
        if (!milestones.Any()) return null;
        var lines = milestones.Take(BannerLineCap).Select(MilestoneLine).ToList();
        if (milestones.Count > BannerLineCap)
            lines.Add($"…and {milestones.Count - BannerLineCap} more milestones");
        return string.Join("\n", lines);
    }

    private static string MilestoneLine(PlayerMilestoneRecord m)
    {
        var detail = (m.Detail ?? string.Empty).Split('|');
        return m.Kind switch
        {
            MilestoneKind.FolderPassLamp => $"🎉 #DIFFICULTY|{detail[0]}# **All passed!**",
            MilestoneKind.FolderGradeLamp when detail.Length == 2 =>
                $"🏆 #DIFFICULTY|{detail[0]}# **All {detail[1]} or better**",
            MilestoneKind.FolderPlateLamp when detail.Length == 2 =>
                $"🏆 #DIFFICULTY|{detail[0]}# **All #PLATE|{detail[1]}# or better**",
            MilestoneKind.TitleCompleted => $"🏅 **{m.Title}** completed",
            MilestoneKind.ParagonLevelGain => $"🏅 **{m.Title}** paragon → {m.Detail}",
            MilestoneKind.PumbilityGain =>
                $"📈 **PUMBILITY {m.OldValue:N0} → {m.NewValue:N0}** (+{m.NewValue - m.OldValue:N0})",
            MilestoneKind.SinglesCompetitiveGain =>
                $"📈 **Singles competitive {m.OldValue:0.00} → {m.NewValue:0.00}**",
            MilestoneKind.DoublesCompetitiveGain =>
                $"📈 **Doubles competitive {m.OldValue:0.00} → {m.NewValue:0.00}**",
            _ => $"🏆 {m.Detail}"
        };
    }

    // The card frame takes the best changed grade's color; the mix stays a textual
    // prefix + emoji, never the accent (locked decision).
    private static uint AccentFor(IEnumerable<RecordedPhoenixScore> bests)
    {
        var top = bests.Where(b => b.Score != null).Select(b => b.Score!.Value.LetterGrade)
            .DefaultIfEmpty(PhoenixLetterGrade.A).Max();
        return top >= PhoenixLetterGrade.SSS ? 0xE8C24Au
            : top >= PhoenixLetterGrade.S ? 0xAEB6C4u
            : 0x6E8CA0u;
    }

    private static string Charts(int count)
    {
        return count == 1 ? "chart" : "charts";
    }

    public async Task Consume(ConsumeContext<UserUpdatedEvent> context)
    {
        if (context.Message.IsPublic)
            await JoinSystemCommunity("World", context.Message.UserId, context.CancellationToken);
        else if (await _communities.GetCommunityByName("World", context.CancellationToken) != null)
            await _mediator.Send(new LeaveCommunityCommand("World", context.Message.UserId));

        if (context.Message.Country != null)
            await JoinSystemCommunity(context.Message.Country, context.Message.UserId, context.CancellationToken);
    }

    // System communities (World + one per country) have no seed anywhere — a fresh
    // database throws CommunityNotFoundException on the first profile update. They
    // create themselves on first join instead: public, regional, unowned.
    private async Task JoinSystemCommunity(Name name, Guid userId, CancellationToken cancellationToken)
    {
        if (await _communities.GetCommunityByName(name, cancellationToken) == null)
            await _communities.SaveCommunity(new Community(name, Guid.Empty, CommunityPrivacyType.Public, true),
                cancellationToken);

        await _mediator.Send(new JoinCommunityCommand(name, null, userId));
    }

    public async Task Consume(ConsumeContext<UserWeeklyChartsProgressedEvent> context)
    {
        var user = await _users.GetUser(context.Message.UserId, context.CancellationToken) ??
                   throw new Exception("User not found");
        var chart = await _charts.GetChart(context.Message.Mix, context.Message.ChartId) ??
                    throw new ChartNotFoundException();

        await SendToCommunityDiscords(context.Message.UserId,
            MixPrefix(context.Message.Mix) +
            $"{user.Name} progressed to {context.Message.Place} on {chart.Song.Name} #DIFFICULTY|{chart.DifficultyString}# - {context.Message.Score:N0} #LETTERGRADE|{PhoenixScore.From(context.Message.Score).LetterGrade}|{context.Message.IsBroken}# #PLATE|{context.Message.Plate}#",
            context.CancellationToken);
    }

    public async Task Handle(AddDiscordChannelToCommunityCommand request, CancellationToken cancellationToken)
    {
        var community = await LoadCommunity(request.CommunityName, request.InviteCode, cancellationToken);

        foreach (var existingChannel in community.Channels.Where(c => c.ChannelId == request.ChannelId).ToArray())
            community.Channels.Remove(existingChannel);

        community.Channels.Add(new Community.ChannelConfiguration(request.ChannelId, request.SendScores,
            request.SendTitles, request.SendNewMembers));
        await _communities.SaveCommunity(community, cancellationToken);

        await _bot.SendMessage(
            $"This channel was updated to receive notifications for the {community.Name} community in PIU Scores!",
            request.ChannelId, cancellationToken);
    }

    public async Task Handle(CreateCommunityCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.User.Id;
        var community = await _communities.GetCommunityByName(request.CommunityName, cancellationToken);
        if (community != null) throw new CommunityAlreadyExistsException(request.CommunityName);
        community = new Community(request.CommunityName, userId, request.PrivacyType, false);
        community.MemberIds.Add(userId);
        await _communities.SaveCommunity(community,
            cancellationToken);
    }

    public async Task<Guid> Handle(CreateInviteLinkCommand request, CancellationToken cancellationToken)
    {
        var community = await GetCommunity(request.CommunityName, cancellationToken);
        if (!community.MemberIds.Contains(_currentUser.User.Id))
            throw new DeniedFromCommunityException(
                "You must be a member of a community to create invite links for it");

        var newCode = Guid.NewGuid();
        community.InviteCodes[newCode] = request.ExpirationDate;
        await _communities.SaveCommunity(community, cancellationToken);
        return newCode;
    }

    public async Task<IEnumerable<CommunityLeaderboardRecord>> Handle(GetCommunityLeaderboardQuery request,
        CancellationToken cancellationToken)
    {
        var community = await GetCommunity(request.Community, cancellationToken);
        if (community.PrivacyType == CommunityPrivacyType.Private && !(_currentUser.IsLoggedIn &&
                                                                       community.MemberIds.Contains(_currentUser
                                                                           .User.Id)))
            throw new DeniedFromCommunityException("This community is private and you must be a member to view it");

        return await _communities.GetLeaderboard(request.Mix, request.Community, cancellationToken);
    }

    public async Task<Community> Handle(GetCommunityQuery request, CancellationToken cancellationToken)
    {
        var community = await GetCommunity(request.CommunityName, cancellationToken);
        if (community.PrivacyType == CommunityPrivacyType.Private &&
            !(_currentUser.IsLoggedIn && community.MemberIds.Contains(_currentUser.User.Id)))
            throw new CommunityNotFoundException();

        return community;
    }

    public async Task<IEnumerable<CommunityOverviewRecord>> Handle(GetMyCommunitiesQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.IsLoggedIn)
            return await _communities.GetCommunities(_currentUser.User.Id, cancellationToken);

        return await _communities.GetPublicCommunities(cancellationToken);
    }

    public async Task<IEnumerable<UserPhoenixScore>> Handle(GetPhoenixRecordsForCommunityQuery request,
        CancellationToken cancellationToken)
    {
        var community = await GetCommunity(request.CommuityName, cancellationToken);
        return await _scores.GetPhoenixScores(request.Mix, community.MemberIds, request.ChartId,
            cancellationToken);
    }

    public async Task<IEnumerable<CommunityOverviewRecord>> Handle(GetPublicCommunitiesQuery request,
        CancellationToken cancellationToken)
    {
        return await _communities.GetPublicCommunities(cancellationToken);
    }

    public async Task<IEnumerable<Guid>> Handle(GetCommunityMembersQuery request,
        CancellationToken cancellationToken)
    {
        var community = await _communities.GetCommunityByName(request.CommunityName, cancellationToken);
        return community?.MemberIds ?? (IEnumerable<Guid>)Array.Empty<Guid>();
    }

    public async Task Handle(JoinCommunityByInviteCodeCommand request, CancellationToken cancellationToken)
    {
        var community = await _communities.GetCommunityByInviteCode(request.InviteCode, cancellationToken);
        if (community == null) throw new CommunityNotFoundException();
        await Handle(new JoinCommunityCommand(community.Value, request.InviteCode), cancellationToken);
    }

    public async Task Handle(JoinCommunityCommand request, CancellationToken cancellationToken)
    {
        var userId = request.UserId ?? _currentUser.User.Id;
        var community = await GetCommunity(request.CommunityName, cancellationToken);

        if (community.MemberIds.Contains(userId)) return;

        switch (community.PrivacyType)
        {
            case CommunityPrivacyType.Public:
                community.MemberIds.Add(userId);
                break;
            case CommunityPrivacyType.Private:
            case CommunityPrivacyType.PublicWithCode:
                var code = request.InviteCode ??
                           throw new DeniedFromCommunityException("This community requires an invite code");
                if (!community.InviteCodes.ContainsKey(code))
                    throw new DeniedFromCommunityException(
                        "This is not a valid community code for this community.");

                if (community.InviteCodes.TryGetValue(code, out var expirationDate) && expirationDate <
                    new DateOnly(_dateTime.Now.Year, _dateTime.Now.Month, _dateTime.Now.Day))
                    throw new DeniedFromCommunityException("This invite code is expired");

                community.MemberIds.Add(userId);
                break;
            default:
                throw new DeniedFromCommunityException("Community privacy type could not be determined");
        }

        await _communities.SaveCommunity(community, cancellationToken);
    }

    public async Task Handle(LeaveCommunityCommand request, CancellationToken cancellationToken)
    {
        var userId = request.UserId ?? _currentUser.User.Id;
        var community = await GetCommunity(request.CommunityName, cancellationToken);
        if (!community.MemberIds.Contains(userId)) return;

        community.MemberIds.Remove(userId);
        await _communities.SaveCommunity(community, cancellationToken);
    }

    public async Task Handle(RemoveDiscordChannelFromCommunityCommand request, CancellationToken cancellationToken)
    {
        var community = await _communities.GetCommunityByName(request.CommunityName, cancellationToken) ??
                        throw new CommunityNotFoundException();

        foreach (var existingChannel in community.Channels.Where(c => c.ChannelId == request.ChannelId).ToArray())
            community.Channels.Remove(existingChannel);

        await _communities.SaveCommunity(community, cancellationToken);

        await _bot.SendMessage(
            $"This channel was **removed** to receive notifications for the {community.Name} community in PIU Scores",
            request.ChannelId, cancellationToken);
    }

    private async Task<Community> GetCommunity(Name name, CancellationToken cancellationToken)
    {
        var community = await _communities.GetCommunityByName(name, cancellationToken);
        return community ?? throw new CommunityNotFoundException();
    }

    private async Task<Community> LoadCommunity(Name? communityName, Guid? inviteCode,
        CancellationToken cancellationToken)
    {
        if (inviteCode != null)
            communityName = await _communities.GetCommunityByInviteCode(inviteCode.Value, cancellationToken) ??
                            throw new CommunityNotFoundException();

        if (communityName == null)
            throw new InvalidOperationException("Community Name must be provided if invite code is not used");

        var community = await _communities.GetCommunityByName(communityName.Value, cancellationToken) ??
                        throw new CommunityNotFoundException();
        if (community.RequiresCode && (inviteCode == null || !community.InviteCodes.ContainsKey(inviteCode.Value) ||
                                       community.InviteCodes[inviteCode.Value] <
                                       new DateOnly(_dateTime.Now.Year, _dateTime.Now.Month,
                                           _dateTime.Now.Day)))
            throw new CommunityNotFoundException();

        return community;
    }

    // Phoenix stays unprefixed — it is today's default context; the prefix marks the
    // new mix while both run in parallel (plan doc: "[Phoenix 2]" Discord prefix).
    private static string MixPrefix(MixEnum mix)
    {
        return mix == MixEnum.Phoenix ? string.Empty : "[" + mix.GetName() + "] ";
    }

    private async Task SendToCommunityDiscords(Guid userId, string messages, CancellationToken cancellationToken)
    {
        await SendToCommunityDiscords(userId, new[] { messages }, cancellationToken);
    }

    private async Task SendToCommunityDiscords(Guid userId, string[] messages, CancellationToken cancellationToken)
    {
        var channelIds = await GetCommunityChannels(userId, cancellationToken);
        foreach (var message in messages)
            await _bot.SendMessages(new[] { message }, channelIds, cancellationToken);
    }

    private async Task SendRichToCommunityDiscords(Guid userId, IReadOnlyList<RichBotMessage> messages,
        CancellationToken cancellationToken)
    {
        if (!messages.Any()) return;
        var channelIds = await GetCommunityChannels(userId, cancellationToken);
        if (!channelIds.Any()) return;
        await _bot.SendRichMessages(messages, channelIds, cancellationToken);
    }

    private async Task<IReadOnlyList<ulong>> GetCommunityChannels(Guid userId, CancellationToken cancellationToken)
    {
        var communities =
            await _communities.GetCommunities(userId, cancellationToken);
        var channelIds = new List<ulong>();
        foreach (var communityName in communities.Select(c => c.CommunityName))
        {
            var community = await _communities.GetCommunityByName(communityName, cancellationToken);
            if (community == null) continue;

            channelIds.AddRange(community.Channels.Select(c => c.ChannelId));
        }

        return channelIds.Distinct().ToList();
    }

    public async Task Consume(ConsumeContext<UcsLeaderboardPlacedEvent> context)
    {
        var user = await _users.GetUser(context.Message.UserId);
        if (user == null) return;
        var placed = context.Message;
        var message =
            $"{user.Name} scored {placed.Score} #LETTERGRADE|{PhoenixScore.From(placed.Score).LetterGrade}|{placed.IsBroken}# on {placed.Artist}'s {placed.SongName} #DIFFICULTY|{placed.Difficulty}# UCS";
        await SendToCommunityDiscords(context.Message.UserId, message, context.CancellationToken);
    }
}