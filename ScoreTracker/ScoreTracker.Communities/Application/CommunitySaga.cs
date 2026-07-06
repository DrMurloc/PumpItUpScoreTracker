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
using ScoreTracker.WeeklyChallenge.Contracts;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;

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
    IConsumer<ScoreHighlightsCapturedEvent>,
    IConsumer<NewTitlesAcquiredEvent>,
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

    // The session snapshot (design doc revision 2): ONE card per score batch — stats
    // that moved, achievements earned, and only the scores worth reading; everything
    // else is a count. Renders from ScoreHighlightsCapturedEvent, which the capture
    // orchestrator publishes AFTER the rating/title steps ran, so every section is
    // deterministic. This is the only score-triggered community Discord message; the
    // old ratings/weekly messages are retired (titles keep a legacy announcement only
    // for site-detected titles, which no card covers).
    private const int ArtRowCap = 5;
    private const int InlineRowCap = 10;
    private const int CoOpRowCap = 3;
    private const int WeeklyLineCap = 4;
    private const int TitleNameCap = 10;
    private const int ProgressDeltaCap = 3;
    private const int FolderLineCap = 6;
    private const int BigGainThreshold = 10000;
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

        var known = e.Changes
            .Where(c => charts.ContainsKey(c.ChartId) && bests.ContainsKey(c.ChartId))
            .ToArray();
        if (!known.Any()) return;

        // The 💥 row: the session's single biggest upscore, when it cleared the
        // threshold (owner call: +10k). It earns a row even with no other flag.
        var bigGain = known
            .Where(c => !c.IsBroken && c.OldScore != null && c.NewScore != null &&
                        c.NewScore.Value - c.OldScore.Value >= BigGainThreshold)
            .OrderByDescending(c => c.NewScore!.Value - c.OldScore!.Value)
            .FirstOrDefault();

        bool Notable(ScoreHighlightsCapturedEvent.HighlightedChange c)
        {
            return c.Flags != HighlightFlag.None || ReferenceEquals(c, bigGain);
        }

        // Notable rows lead and own the art; within each group the universal noteworthy
        // ordering applies — difficulty desc, scoring level desc, score desc (the same
        // composite the Sessions page uses).
        var standard = known
            .Where(c => charts[c.ChartId].Type != ChartType.CoOp)
            .OrderByDescending(Notable)
            .ThenByDescending(c => (int)charts[c.ChartId].Level)
            .ThenByDescending(c => scoringLevels.TryGetValue(c.ChartId, out var sl) ? sl : 0)
            .ThenByDescending(c => (int)(bests[c.ChartId].Score ?? 0))
            .ToArray();
        var notable = standard.Where(Notable).Take(InlineRowCap).ToArray();

        // Co-ops always show (owner call) — they can't earn the S/D flags, so they get
        // their own rows: up to 3, community co-op difficulty rating descending.
        var coOpChanges = known.Where(c => charts[c.ChartId].Type == ChartType.CoOp).ToArray();
        var coOpRows = Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>();
        if (coOpChanges.Any())
        {
            var coOpRatings = (await _mediator.Send(new GetCoOpRatingsQuery(), context.CancellationToken))
                .ToDictionary(r => r.ChartId, r => r.Ratings.Any() ? r.Ratings.Values.Max(l => (int)l) : 0);
            coOpRows = coOpChanges
                .OrderByDescending(c => coOpRatings.GetValueOrDefault(c.ChartId, 0))
                .ThenByDescending(c => (int)(bests[c.ChartId].Score ?? 0))
                .Take(CoOpRowCap)
                .ToArray();
        }

        // The weekly read: current placements for whichever batch charts sit on this
        // week's board. Failure costs the weekly lines, never the card.
        var weekly = Array.Empty<WeeklyPlacementRecord>();
        try
        {
            weekly = (await _mediator.Send(new GetUserWeeklyPlacementsQuery(e.UserId, e.Mix,
                    known.Select(c => c.ChartId).Distinct().ToArray()), context.CancellationToken))
                .OrderByDescending(w => (int)charts[w.ChartId].Level)
                .Take(WeeklyLineCap)
                .ToArray();
        }
        catch
        {
            // The board read is a flex, not a fact the card owes anyone.
        }

        var message = await BuildSnapshotCard(e, user, known, notable, coOpRows, charts, bests, weekly,
            context.CancellationToken);
        await SendRichToCommunityDiscords(user.Id, new[] { message }, context.CancellationToken);
    }

    private async Task<RichBotMessage> BuildSnapshotCard(ScoreHighlightsCapturedEvent e, User user,
        ScoreHighlightsCapturedEvent.HighlightedChange[] known,
        ScoreHighlightsCapturedEvent.HighlightedChange[] notable,
        ScoreHighlightsCapturedEvent.HighlightedChange[] coOpRows, IDictionary<Guid, Chart> charts,
        IDictionary<Guid, RecordedPhoenixScore> bests, WeeklyPlacementRecord[] weekly,
        CancellationToken cancellationToken)
    {
        var blocks = new List<IRichBotBlock> { new RichBotDivider() };

        // ① Stats that moved (capture already floored the noise).
        var stats = StatLines(e.Milestones);
        if (stats.Any())
        {
            blocks.Add(new RichBotText(string.Join("\n", stats)));
            blocks.Add(new RichBotDivider());
        }

        // ② Achievements: titles, paragon, folder lamps, weekly placements — or the
        // per-title progress deltas when nothing completed.
        var achievements = AchievementLines(e, weekly, charts);
        if (achievements.Any())
        {
            blocks.Add(new RichBotText(string.Join("\n", achievements)));
            blocks.Add(new RichBotDivider());
        }

        // ③ Notable scores: art while the slots last, individual text rows after.
        var artLeft = ArtRowCap;
        foreach (var change in notable)
            blocks.Add(Row(change, charts, bests, bigGain: IsBigGain(change, e, known), ref artLeft));
        foreach (var change in coOpRows)
            blocks.Add(Row(change, charts, bests, bigGain: IsBigGain(change, e, known), ref artLeft));

        var shown = notable.Concat(coOpRows).Select(c => c.ChartId).ToHashSet();
        var rest = known.Where(c => !shown.Contains(c.ChartId)).ToArray();
        if (rest.Any())
        {
            var parts = rest
                .Where(c => charts[c.ChartId].Type != ChartType.CoOp)
                .GroupBy(c => charts[c.ChartId].DifficultyString)
                .OrderByDescending(g => (int)charts[g.First().ChartId].Level)
                .Select(g => g.Count() == 1 ? g.Key : $"{g.Key} ×{g.Count()}")
                .ToList();
            var restCoOps = rest.Count(c => charts[c.ChartId].Type == ChartType.CoOp);
            if (restCoOps > 0) parts.Add($"CO-OP ×{restCoOps}");
            blocks.Add(new RichBotText($"+{rest.Length} more: {string.Join(", ", parts)}"));
        }

        var passCharts = known
            .Where(c => c.IsNewPass && !c.IsBroken)
            .Select(c => charts[c.ChartId])
            .Where(c => c.Type is ChartType.Single or ChartType.Double);
        var folderStats = await FolderProgress(e.Mix, e.UserId, passCharts, FolderLineCap, cancellationToken);
        if (!string.IsNullOrWhiteSpace(folderStats))
        {
            blocks.Add(new RichBotDivider());
            blocks.Add(new RichBotText(folderStats));
        }

        var passes = known.Count(c => c.IsNewPass && !c.IsBroken);
        var upscores = known.Count(c => !c.IsNewPass && !c.IsBroken);
        var counts = passes > 0 && upscores > 0
            ? $"passed {passes:N0} · upscored {upscores:N0}"
            : passes > 0
                ? $"passed {passes:N0} {Charts(passes)}"
                : upscores > 0
                    ? $"upscored {upscores:N0} {Charts(upscores)}"
                    : $"updated {known.Length:N0} {Charts(known.Length)}";
        var span = LevelSpan(known
            .Where(c => charts[c.ChartId].Type != ChartType.CoOp)
            .Select(c => charts[c.ChartId]).ToArray());
        if (coOpRows.Any() || known.Any(c => charts[c.ChartId].Type == ChartType.CoOp))
            span = span.Length > 0 ? $"{span} · CO-OP" : "CO-OP";
        var headerMarkdown = $"### {MixPrefix(e.Mix)}**{user.Name}** — {counts}" +
                             (span.Length > 0 ? $"\n-# {span}" : string.Empty);

        // The deep link only renders for public players — the Sessions page redirects
        // everyone else home anyway.
        var links = user.IsPublic
            ? new[]
            {
                new RichBotLink("See more",
                    new Uri($"{SiteBase}/Player/{user.Id}/Sessions" +
                            (e.SessionId == null ? string.Empty : $"?session={e.SessionId}")))
            }
            : Array.Empty<RichBotLink>();

        return new RichBotMessage(new RichBotSection(headerMarkdown, user.ProfileImage), blocks,
            $"#MIX|{e.Mix}# {e.Mix.GetName()} · PIU Scores",
            e.Mix.GetAccentColor(), links);
    }

    private static bool IsBigGain(ScoreHighlightsCapturedEvent.HighlightedChange change,
        ScoreHighlightsCapturedEvent e, ScoreHighlightsCapturedEvent.HighlightedChange[] known)
    {
        if (change.IsBroken || change.OldScore == null || change.NewScore == null) return false;
        var gain = change.NewScore.Value - change.OldScore.Value;
        if (gain < BigGainThreshold) return false;
        return gain == known
            .Where(c => !c.IsBroken && c.OldScore != null && c.NewScore != null)
            .Max(c => c.NewScore!.Value - c.OldScore!.Value);
    }

    private IRichBotBlock Row(ScoreHighlightsCapturedEvent.HighlightedChange change,
        IDictionary<Guid, Chart> charts, IDictionary<Guid, RecordedPhoenixScore> bests, bool bigGain,
        ref int artLeft)
    {
        var chart = charts[change.ChartId];
        var text = RowText(change, chart, bests[change.ChartId], bigGain);
        if (artLeft <= 0) return new RichBotText(text);
        artLeft--;
        return new RichBotSection(text, chart.Song.ImagePath);
    }

    private static string RowText(ScoreHighlightsCapturedEvent.HighlightedChange change, Chart chart,
        RecordedPhoenixScore best, bool bigGain)
    {
        return change.IsNewPass ? PassRow(change, chart, best, bigGain) : UpscoreRow(change, chart, best, bigGain);
    }

    private static IReadOnlyList<string> StatLines(IReadOnlyList<PlayerMilestoneRecord> milestones)
    {
        var lines = new List<string>();
        foreach (var m in milestones)
            switch (m.Kind)
            {
                case MilestoneKind.PumbilityGain:
                    lines.Add($"📈 **PUMBILITY** {m.OldValue:N0} → **{m.NewValue:N0}** " +
                              $"(+{m.NewValue - m.OldValue:N0})");
                    break;
                case MilestoneKind.SinglesCompetitiveGain:
                    lines.Add($"📈 **Singles competitive** {m.OldValue:0.00} → **{m.NewValue:0.00}**");
                    break;
                case MilestoneKind.DoublesCompetitiveGain:
                    lines.Add($"📈 **Doubles competitive** {m.OldValue:0.00} → **{m.NewValue:0.00}**");
                    break;
            }

        return lines;
    }

    private static IReadOnlyList<string> AchievementLines(ScoreHighlightsCapturedEvent e,
        WeeklyPlacementRecord[] weekly, IDictionary<Guid, Chart> charts)
    {
        var lines = new List<string>();
        var titles = e.Milestones.Where(m => m.Kind == MilestoneKind.TitleCompleted).ToArray();
        lines.AddRange(titles.Take(TitleNameCap).Select(t => $"🏅 **{t.Title}** completed"));
        if (titles.Length > TitleNameCap)
            lines.Add($"…and {titles.Length - TitleNameCap} more titles");

        // Paragon gains are never counted or aggregated — the new grade IS the content
        // (owner call), so every gain is its own grade-named line.
        var paragons = e.Milestones.Where(m => m.Kind == MilestoneKind.ParagonLevelGain).ToArray();
        lines.AddRange(paragons.Select(p => $"🏅 **{p.Title}** paragon → {ParagonEmoji(p.Detail)}"));

        foreach (var lamp in e.Milestones.Where(m => m.Kind is MilestoneKind.FolderPassLamp
                     or MilestoneKind.FolderGradeLamp or MilestoneKind.FolderPlateLamp))
            lines.Add(LampLine(lamp));

        lines.AddRange(weekly.Select(w =>
            $"🏆 **#{w.Place}** on {charts[w.ChartId].Song.Name} " +
            $"#DIFFICULTY|{charts[w.ChartId].DifficultyString}# weekly"));

        // The nothing-completed fallback: real per-title progress deltas (owner call),
        // nearest to complete first.
        if (!titles.Any() && !paragons.Any())
            lines.AddRange(e.TitleProgress.Take(ProgressDeltaCap).Select(d =>
                $"🏅 {d.Title} {(int)(d.OldPercent * 100)}% → **{(int)(d.NewPercent * 100)}%**"));

        return lines;
    }

    private static string ParagonEmoji(string? detail)
    {
        return detail switch
        {
            null or "" or "None" => detail ?? string.Empty,
            "PG" => "#PLATE|PerfectGame#",
            _ => $"#LETTERGRADE|{detail}|False#"
        };
    }

    private static string LampLine(PlayerMilestoneRecord m)
    {
        var detail = (m.Detail ?? string.Empty).Split('|');
        return m.Kind switch
        {
            MilestoneKind.FolderPassLamp => $"🎉 #DIFFICULTY|{detail[0]}# **All passed!**",
            MilestoneKind.FolderGradeLamp when detail.Length == 2 =>
                $"🏆 #DIFFICULTY|{detail[0]}# **All {detail[1]} or better**",
            MilestoneKind.FolderPlateLamp when detail.Length == 2 =>
                $"🏆 #DIFFICULTY|{detail[0]}# **All #PLATE|{detail[1]}# or better**",
            _ => $"🏆 {m.Detail}"
        };
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
        RecordedPhoenixScore best, bool bigGain)
    {
        return $"#DIFFICULTY|{chart.DifficultyString}# {SongLink(change, chart, bigGain)}\n" +
               $"**{(int)best.Score!.Value:N0}** #LETTERGRADE|{best.Score!.Value.LetterGrade}|{best.IsBroken}##PLATE|{best.Plate}#" +
               FlagCaption(change.Flags, bigGain);
    }

    private static string UpscoreRow(ScoreHighlightsCapturedEvent.HighlightedChange change, Chart chart,
        RecordedPhoenixScore best, bool bigGain)
    {
        var row = $"#DIFFICULTY|{chart.DifficultyString}# {SongLink(change, chart, bigGain)} " +
                  $"**{(int)best.Score!.Value:N0}**";
        if (change.OldScore != null)
        {
            row += $" (+{(int)best.Score!.Value - change.OldScore.Value:N0})";
            var oldLetter = PhoenixScore.From(change.OldScore.Value).LetterGrade;
            if (oldLetter != best.Score!.Value.LetterGrade)
                row += $" #LETTERGRADE|{oldLetter}|False# →";
        }

        return row + $" #LETTERGRADE|{best.Score!.Value.LetterGrade}|{best.IsBroken}##PLATE|{best.Plate}#" +
               FlagCaption(change.Flags, bigGain);
    }

    private static string SongLink(ScoreHighlightsCapturedEvent.HighlightedChange change, Chart chart,
        bool bigGain)
    {
        var link = $"[{chart.Song.Name}]({SiteBase}/Chart/{chart.Id})";
        return change.Flags == HighlightFlag.None && !bigGain ? link : $"**{link}**";
    }

    // The why-it's-noteworthy caption, rendered as Discord subtext under the score.
    // Vocabulary mirrors the Sessions page badge tooltips.
    private static string FlagCaption(HighlightFlag flags, bool bigGain)
    {
        if (flags == HighlightFlag.None && !bigGain) return string.Empty;
        var parts = new List<string>();
        if (flags.HasFlag(HighlightFlag.PumbilityTop50)) parts.Add("👑 PUMBILITY top 50");
        if (flags.HasFlag(HighlightFlag.ScoreQuality90)) parts.Add("📊 Top scores among peers");
        if (flags.HasFlag(HighlightFlag.TitleProgress)) parts.Add("🏅 Title progress");
        if (flags.HasFlag(HighlightFlag.FolderDebut)) parts.Add("🆕 Folder debut");
        if (flags.HasFlag(HighlightFlag.FolderCompletion90)) parts.Add("📁 Nearly complete folder");
        if (flags.HasFlag(HighlightFlag.CompetitiveImprover)) parts.Add("⬆ Raised competitive level");
        if (bigGain) parts.Add("💥 Biggest gain of the session");
        return "\n-# " + string.Join(" · ", parts);
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